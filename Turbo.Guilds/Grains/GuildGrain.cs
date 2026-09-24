using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Guilds.Configuration;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Grains;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Guilds.Grains;

/// <summary>
/// One group. Loaded on activation together with its roster, which is what lets a view answer
/// from memory; the group row and the membership rows are the truth, and every change to either
/// goes through this grain so the copy here cannot drift from them.
///
/// The roster holds ids and ranks only. Names and figures belong to the players and are
/// resolved when a packet needs them, so a group of five thousand costs five thousand ints
/// rather than five thousand profiles.
///
/// The one grain it does call is the directory, for the badge palette its colour ids point
/// into. The directory never calls back into a group, so that direction is safe; the room grain
/// is the one that must stay out of reach.
/// </summary>
internal sealed partial class GuildGrain : Grain, IGuildGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly IGrainFactory _grainFactory;
    private readonly GuildConfig _guildConfig;
    private readonly ILogger<IGuildGrain> _logger;

    private readonly GuildLiveState _state = new();

    public GuildGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IGrainFactory grainFactory,
        IOptions<GuildConfig> guildConfig,
        ILogger<IGuildGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _grainFactory = grainFactory;
        _guildConfig = guildConfig.Value;
        _logger = logger;
    }

    private GuildId GuildId => GuildId.Parse((int)this.GetPrimaryKeyLong());

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load guild {GuildId}", GuildId.Value);

            throw;
        }
    }

    public Task<GuildSnapshot?> GetSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(_state.Guild);

    public Task<GuildViewSnapshot?> GetViewAsync(PlayerId viewerId, CancellationToken ct)
    {
        if (_state.Guild is not { } guild)
            return Task.FromResult<GuildViewSnapshot?>(null);

        var rank = GetRank(viewerId);
        var isOwner = guild.OwnerId == viewerId;
        var isAdmin = rank == GuildMemberRank.Admin;

        return Task.FromResult<GuildViewSnapshot?>(
            new GuildViewSnapshot
            {
                Guild = guild,
                Description = guild.Description,
                CreatedAt = guild.CreatedAt,
                MemberCount = CountOfRanks(
                    GuildMemberRank.Owner,
                    GuildMemberRank.Admin,
                    GuildMemberRank.Member
                ),
                PendingMemberCount =
                    isOwner || isAdmin ? CountOfRanks(GuildMemberRank.Requested) : 0,
                Status = ToStatus(rank),
                IsOwner = isOwner,
                IsAdmin = isAdmin,
                MembersCanDecorate = guild.RightsLevel == GuildRightsLevel.Members,
            }
        );
    }

    public Task<GuildMemberRank?> GetMemberRankAsync(PlayerId playerId, CancellationToken ct) =>
        Task.FromResult(GetRank(playerId));

    private GuildMemberRank? GetRank(PlayerId playerId) =>
        _state.RankByPlayerId.TryGetValue(playerId.Value, out var rank) ? rank : null;

    /// <summary>
    /// What the client calls status: whether the viewer is in the group, waiting, or neither. A
    /// blocked player is told they are not a member, which is the thing they can act on.
    /// </summary>
    private static GuildMembershipStatus ToStatus(GuildMemberRank? rank) =>
        rank switch
        {
            GuildMemberRank.Owner or GuildMemberRank.Admin or GuildMemberRank.Member =>
                GuildMembershipStatus.Member,
            GuildMemberRank.Requested => GuildMembershipStatus.Pending,
            _ => GuildMembershipStatus.NotMember,
        };

    private int CountOfRanks(params GuildMemberRank[] ranks) =>
        _state.RankByPlayerId.Values.Count(ranks.Contains);

    private async Task LoadAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entity = await dbCtx
            .Guilds.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == GuildId.Value, ct);

        _state.IsLoaded = true;

        if (entity is null)
            return;

        var members = await dbCtx
            .GuildMembers.AsNoTracking()
            .Where(x => x.GuildEntityId == GuildId.Value)
            .Select(x => new { x.PlayerEntityId, x.Rank })
            .ToListAsync(ct);

        var editorData = await _grainFactory.GetGuildDirectoryGrain().GetEditorDataAsync(ct);

        // No group has a forum until the forum ship lands.
        _state.Guild = entity.ToSnapshot(
            hasForum: false,
            editorData.GetColor(GuildColorSlotType.Primary, entity.PrimaryColorId),
            editorData.GetColor(GuildColorSlotType.Secondary, entity.SecondaryColorId)
        );

        _state.RankByPlayerId.Clear();

        foreach (var member in members)
            _state.RankByPlayerId[member.PlayerEntityId] = member.Rank;
    }
}
