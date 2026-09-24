using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Guilds.Grains;

/// <summary>
/// Every group in the hotel, one grain: name, badge, homeroom and owner. It is a read-through
/// cache over <c>guilds</c>, loaded on activation and again on a timer, with the group grains
/// telling it about their own changes in between. Nothing here is written back.
///
/// Every room activation asks it whether it is somebody's homeroom, and every badge drawn asks
/// it for a code, so its methods answer from memory and the only queries are the reload. Kept
/// alive because reactivating means reading every group again while all of those reads wait.
/// </summary>
[KeepAlive]
internal sealed class GuildDirectoryGrain : Grain, IGuildDirectoryGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly GuildConfig _guildConfig;
    private readonly ILogger<IGuildDirectoryGrain> _logger;

    /// <summary>The middle cell of the 3x3 grid, where a base part belongs.</summary>
    private const int DEFAULT_BADGE_POSITION = 4;

    private readonly GuildDirectoryLiveState _state = new();

    private IDisposable? _refreshTimer;

    public GuildDirectoryGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<GuildConfig> guildConfig,
        ILogger<IGuildDirectoryGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _guildConfig = guildConfig.Value;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate the guild directory");

            throw;
        }

        _refreshTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((GuildDirectoryGrain)self!).RefreshAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_guildConfig.DirectoryRefreshMs),
            TimeSpan.FromMilliseconds(_guildConfig.DirectoryRefreshMs)
        );
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;

        return Task.CompletedTask;
    }

    public Task<ImmutableArray<GuildSummarySnapshot>> GetSummariesAsync(
        ImmutableArray<GuildId> guildIds,
        CancellationToken ct
    ) =>
        Task.FromResult(
            guildIds
                .Select(guildId => _state.SummaryByGuildId.GetValueOrDefault(guildId.Value))
                .OfType<GuildSummarySnapshot>()
                .ToImmutableArray()
        );

    public Task<GuildSummarySnapshot?> GetSummaryAsync(GuildId guildId, CancellationToken ct) =>
        Task.FromResult(_state.SummaryByGuildId.GetValueOrDefault(guildId.Value));

    public Task<GuildSummarySnapshot?> GetGuildOfRoomAsync(RoomId roomId, CancellationToken ct) =>
        Task.FromResult(
            _state.GuildIdByRoomId.TryGetValue(roomId.Value, out var guildId)
                ? _state.SummaryByGuildId.GetValueOrDefault(guildId)
                : null
        );

    /// <summary>
    /// Ordered by member count, which the timer refreshes rather than the joins themselves. A
    /// listing of the hotel's biggest groups does not need to be to the minute, and paying a
    /// grain call per join to keep it so would not be worth it.
    /// </summary>
    public Task<ImmutableArray<RoomId>> GetGuildBaseRoomIdsAsync(CancellationToken ct) =>
        Task.FromResult(
            _state
                .SummaryByGuildId.Values.OrderByDescending(summary =>
                    _state.MemberCountByGuildId.GetValueOrDefault(summary.GuildId.Value)
                )
                .ThenByDescending(summary => summary.GuildId.Value)
                .Take(_guildConfig.GuildBaseSearchResultLimit)
                .Select(summary => summary.RoomId)
                .ToImmutableArray()
        );

    public Task<GuildEditorDataSnapshot> GetEditorDataAsync(CancellationToken ct) =>
        Task.FromResult(_state.EditorData);

    /// <summary>
    /// The first base part in the first badge colour, centred. Deterministic on purpose: the
    /// creator is about to open the editor on it, and a badge that differs every time the
    /// wizard is reopened is a badge they cannot get back.
    /// </summary>
    public Task<ImmutableArray<GuildBadgePartSnapshot>> GetDefaultBadgePartsAsync(
        CancellationToken ct
    )
    {
        var basePart = _state.EditorData.BaseParts.FirstOrDefault();
        var color = _state.EditorData.BadgeColors.FirstOrDefault();

        return Task.FromResult<ImmutableArray<GuildBadgePartSnapshot>>(
            basePart is null || color is null
                ? []
                :
                [
                    new GuildBadgePartSnapshot
                    {
                        Type = GuildBadgePartType.Base,
                        PartId = basePart.PartId,
                        ColorId = color.ColorId,
                        Position = DEFAULT_BADGE_POSITION,
                    },
                ]
        );
    }

    public Task<ImmutableArray<GuildSummarySnapshot>> SearchByNameAsync(
        string query,
        CancellationToken ct
    ) =>
        Task.FromResult(
            string.IsNullOrWhiteSpace(query)
                ? []
                : _state
                    .SummaryByGuildId.Values.Where(summary =>
                        summary.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    )
                    .OrderByDescending(summary =>
                        _state.MemberCountByGuildId.GetValueOrDefault(summary.GuildId.Value)
                    )
                    .Take(_guildConfig.NameSearchResultLimit)
                    .ToImmutableArray()
        );

    public Task OnGuildChangedAsync(GuildSummarySnapshot summary, CancellationToken ct)
    {
        // A group that moved rooms or changed hands would otherwise leave its old keys behind.
        // Neither is possible today, and this is what keeps that from being load-bearing.
        Forget(summary.GuildId);

        _state.SummaryByGuildId[summary.GuildId.Value] = summary;
        _state.GuildIdByRoomId[summary.RoomId.Value] = summary.GuildId.Value;
        _state.OwnedCountByPlayerId[summary.OwnerId.Value] =
            _state.OwnedCountByPlayerId.GetValueOrDefault(summary.OwnerId.Value) + 1;

        return Task.CompletedTask;
    }

    public Task OnGuildRemovedAsync(GuildId guildId, CancellationToken ct)
    {
        Forget(guildId);

        _state.MemberCountByGuildId.Remove(guildId.Value);

        return Task.CompletedTask;
    }

    public Task<int> GetOwnedCountAsync(PlayerId playerId, CancellationToken ct) =>
        Task.FromResult(_state.OwnedCountByPlayerId.GetValueOrDefault(playerId.Value));

    /// <summary>Drops every index entry for this group, leaving its member count alone.</summary>
    private void Forget(GuildId guildId)
    {
        if (!_state.SummaryByGuildId.Remove(guildId.Value, out var previous))
            return;

        _state.GuildIdByRoomId.Remove(previous.RoomId.Value);

        var owned = _state.OwnedCountByPlayerId.GetValueOrDefault(previous.OwnerId.Value) - 1;

        if (owned > 0)
            _state.OwnedCountByPlayerId[previous.OwnerId.Value] = owned;
        else
            _state.OwnedCountByPlayerId.Remove(previous.OwnerId.Value);
    }

    /// <summary>The timer's reload. A failure keeps what it had and tries again next time.</summary>
    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload the guild directory; keeping the previous copy");
        }
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var guilds = await dbCtx.Guilds.AsNoTracking().ToListAsync(ct);
        var memberCounts = await dbCtx
            .GuildMembers.AsNoTracking()
            .Where(x => x.Rank != GuildMemberRank.Requested && x.Rank != GuildMemberRank.Blocked)
            .GroupBy(x => x.GuildEntityId)
            .Select(g => new { GuildEntityId = g.Key, Members = g.Count() })
            .ToListAsync(ct);

        _state.Clear();

        // The palette before the guilds: a guild row stores colour ids and the summary carries
        // the hex, so the mapping below needs it in hand.
        await HydrateEditorDataAsync(dbCtx, ct);

        foreach (var guild in guilds)
        {
            // No group has a forum until the forum ship lands; the client draws no forum link
            // for a group that says false, which is the truth rather than a stub.
            var summary = guild.ToSummarySnapshot(
                hasForum: false,
                _state.EditorData.GetColor(GuildColorSlotType.Primary, guild.PrimaryColorId),
                _state.EditorData.GetColor(GuildColorSlotType.Secondary, guild.SecondaryColorId)
            );

            _state.SummaryByGuildId[guild.Id] = summary;
            _state.GuildIdByRoomId[guild.RoomEntityId] = guild.Id;
            _state.OwnedCountByPlayerId[guild.PlayerEntityId] =
                _state.OwnedCountByPlayerId.GetValueOrDefault(guild.PlayerEntityId) + 1;
        }

        foreach (var count in memberCounts)
            _state.MemberCountByGuildId[count.GuildEntityId] = count.Members;
    }

    private async Task HydrateEditorDataAsync(TurboDbContext dbCtx, CancellationToken ct)
    {
        var parts = await dbCtx
            .GuildBadgeParts.AsNoTracking()
            .OrderBy(x => x.PartId)
            .ToListAsync(ct);
        var colors = await dbCtx.GuildColors.AsNoTracking().OrderBy(x => x.ColorId).ToListAsync(ct);

        _state.EditorData = new GuildEditorDataSnapshot
        {
            BaseParts =
            [
                .. parts
                    .Where(x => x.PartType == GuildBadgePartType.Base)
                    .Select(x => x.ToSnapshot()),
            ],
            SymbolParts =
            [
                .. parts
                    .Where(x => x.PartType == GuildBadgePartType.Symbol)
                    .Select(x => x.ToSnapshot()),
            ],
            BadgeColors =
            [
                .. colors
                    .Where(x => x.Slot == GuildColorSlotType.Badge)
                    .Select(x => x.ToSnapshot()),
            ],
            PrimaryColors =
            [
                .. colors
                    .Where(x => x.Slot == GuildColorSlotType.Primary)
                    .Select(x => x.ToSnapshot()),
            ],
            SecondaryColors =
            [
                .. colors
                    .Where(x => x.Slot == GuildColorSlotType.Secondary)
                    .Select(x => x.ToSnapshot()),
            ],
        };
    }
}
