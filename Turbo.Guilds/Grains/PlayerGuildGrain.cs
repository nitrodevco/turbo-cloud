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
using Turbo.Database.Entities.Guilds;
using Turbo.Database.Entities.Room;
using Turbo.Database.Extensions;
using Turbo.Guilds.Configuration;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains.Guilds;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Texts;

namespace Turbo.Guilds.Grains;

/// <summary>
/// One player's memberships, the badge they wear, and the making of a new group. It holds ids
/// and asks the guild directory for what each group is called and what its badge is, so the two
/// never hold separate copies of a name that can be edited.
///
/// It talks to the directory rather than to the group grains: a player in four hundred groups
/// would otherwise wake four hundred grains to draw one profile.
///
/// It is keyed by player but lives in the guild module, the way <c>CatalogPurchaseGrain</c> is
/// keyed by player and lives in the catalog: what it enforces are the group system's rules, and
/// those belong with the group system.
/// </summary>
internal sealed class PlayerGuildGrain : Grain, IPlayerGuildGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly IGrainFactory _grainFactory;
    private readonly GuildConfig _guildConfig;
    private readonly ILogger<IPlayerGuildGrain> _logger;

    private readonly PlayerGuildLiveState _state = new();

    public PlayerGuildGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IGrainFactory grainFactory,
        IOptions<GuildConfig> guildConfig,
        ILogger<IPlayerGuildGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _grainFactory = grainFactory;
        _guildConfig = guildConfig.Value;
        _logger = logger;
    }

    private PlayerId PlayerId => this.GetPlayerId();

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load guild memberships for {PlayerId}", PlayerId.Value);

            throw;
        }
    }

    public async Task<ImmutableArray<GuildInfoSnapshot>> GetMembershipsAsync(CancellationToken ct)
    {
        if (_state.GuildIds.Count == 0)
            return [];

        var summaries = await _grainFactory
            .GetGuildDirectoryGrain()
            .GetSummariesAsync([.. _state.GuildIds.Select(GuildId.Parse)], ct);

        return
        [
            .. summaries.Select(summary => new GuildInfoSnapshot
            {
                GroupId = summary.GuildId,
                GroupName = summary.Name,
                BadgeCode = summary.BadgeCode,
                PrimaryColor = summary.PrimaryColor,
                SecondaryColor = summary.SecondaryColor,
                Favourite = _state.FavouriteGuildId == summary.GuildId.Value,
                OwnerId = summary.OwnerId,
                HasForum = summary.HasForum,
            }),
        ];
    }

    public Task<GuildId?> GetFavouriteGuildIdAsync(CancellationToken ct) =>
        Task.FromResult(
            _state.FavouriteGuildId is { } guildId ? GuildId.Parse(guildId) : (GuildId?)null
        );

    public async Task<GuildCreationInfoSnapshot> GetCreationInfoAsync(CancellationToken ct)
    {
        var directory = _grainFactory.GetGuildDirectoryGrain();

        var roomsTask = GetRoomOptionsAsync(GuildId.Invalid, ct);
        var badgeTask = directory.GetDefaultBadgePartsAsync(ct);

        await Task.WhenAll(roomsTask, badgeTask);

        return new GuildCreationInfoSnapshot
        {
            CostInCredits = _guildConfig.CreationCostInCredits,
            OwnedRooms = await roomsTask,
            BadgeParts = await badgeTask,
        };
    }

    public async Task<ImmutableArray<GuildRoomOptionSnapshot>> GetRoomOptionsAsync(
        GuildId includeGuildRoomOf,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var rooms = await dbCtx
            .Rooms.AsNoTracking()
            .Where(x => x.PlayerEntityId == PlayerId.Value)
            .Select(x => new
            {
                x.Id,
                x.Name,
                // One query rather than a second read of room_rights per room.
                HasControllers = dbCtx
                    .Set<RoomRightEntity>()
                    .Any(right => right.RoomEntityId == x.Id),
                TakenByGuildId = dbCtx
                    .Guilds.Where(g => g.RoomEntityId == x.Id)
                    .Select(g => (int?)g.Id)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return
        [
            .. rooms
                .Where(x =>
                    x.TakenByGuildId is null || x.TakenByGuildId == includeGuildRoomOf.Value
                )
                .Select(x => new GuildRoomOptionSnapshot
                {
                    RoomId = RoomId.Parse(x.Id),
                    Name = x.Name ?? string.Empty,
                    HasControllers = x.HasControllers,
                }),
        ];
    }

    public async Task<GuildCreationResultSnapshot> CreateGuildAsync(
        GuildCreationRequestSnapshot request,
        CancellationToken ct
    )
    {
        var directory = _grainFactory.GetGuildDirectoryGrain();

        var name = ClientText.Truncate(request.Name, _guildConfig.NameMaxLength);

        if (string.IsNullOrWhiteSpace(name))
            return GuildCreationResultSnapshot.Failed(GuildCreationFailureType.InvalidName);

        if (await directory.GetOwnedCountAsync(PlayerId, ct) >= _guildConfig.OwnedGuildsMax)
            return GuildCreationResultSnapshot.Failed(GuildCreationFailureType.TooManyGroups);

        if (await directory.GetGuildOfRoomAsync(request.RoomId, ct) is not null)
            return GuildCreationResultSnapshot.Failed(GuildCreationFailureType.RoomAlreadyHomeroom);

        // The room must be theirs. The wizard only offers rooms they own, so this is the check
        // against a client that sent an id the wizard never showed it.
        var rooms = await GetRoomOptionsAsync(GuildId.Invalid, ct);

        if (!rooms.Any(x => x.RoomId == request.RoomId))
            return GuildCreationResultSnapshot.Failed(GuildCreationFailureType.RoomAlreadyHomeroom);

        if (
            _guildConfig.CreationRequiresClub
            && !await _grainFactory.HasActiveClubAsync(PlayerId, ct)
        )
            return GuildCreationResultSnapshot.Failed(GuildCreationFailureType.ClubRequired);

        var editorData = await directory.GetEditorDataAsync(ct);
        var badgeCode = GuildBadgeCodes.Build(
            GuildBadgeParts.Sanitize(request.BadgeParts, editorData)
        );

        var primaryColorId = GuildBadgeParts.PickColorId(
            editorData.PrimaryColors,
            request.PrimaryColorId
        );
        var secondaryColorId = GuildBadgeParts.PickColorId(
            editorData.SecondaryColors,
            request.SecondaryColorId
        );

        List<WalletDebitRequest> cost =
            _guildConfig.CreationCostInCredits > 0
                ?
                [
                    new WalletDebitRequest
                    {
                        CurrencyKind = CurrencyKind.Credits,
                        Amount = _guildConfig.CreationCostInCredits,
                    },
                ]
                : [];

        if (cost.Count > 0)
        {
            var debit = await _grainFactory.GetPlayerWalletGrain(PlayerId).TryDebitAsync(cost, ct);

            if (!debit.Succeeded)
                return GuildCreationResultSnapshot.Failed(
                    GuildCreationFailureType.InsufficientCredits
                );
        }

        var entity = new GuildEntity
        {
            Name = name,
            Description = ClientText.Truncate(
                request.Description,
                _guildConfig.DescriptionMaxLength
            ),
            BadgeCode = badgeCode,
            PrimaryColorId = primaryColorId,
            SecondaryColorId = secondaryColorId,
            GuildType = GuildType.Regular,
            RightsLevel = GuildRightsLevel.Admins,
            PlayerEntityId = PlayerId.Value,
            RoomEntityId = request.RoomId.Value,
        };

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            // The group and its owner's membership are inserted together. Two saves would let a
            // group exist with nobody in it, and its owner would then stand in their own
            // homeroom with no rank and so no rights. EF orders the pair and fills the foreign
            // key itself when the member points at the entity rather than at its id.
            dbCtx.Guilds.Add(entity);
            dbCtx.GuildMembers.Add(
                new GuildMemberEntity
                {
                    GuildEntity = entity,
                    GuildEntityId = 0,
                    PlayerEntityId = PlayerId.Value,
                    Rank = GuildMemberRank.Owner,
                    // Their first group becomes the badge they wear; a later one does not take
                    // the place of a badge they chose.
                    IsFavourite = _state.FavouriteGuildId is null,
                }
            );

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create a group for player {PlayerId}; refunding the cost",
                PlayerId.Value
            );

            // They have already been charged. Nothing else gives it back, and a player who paid
            // for a group that does not exist has no way to notice, let alone complain.
            await _grainFactory.RefundAsync(PlayerId, cost, _logger, "creating a group");

            return GuildCreationResultSnapshot.Failed(GuildCreationFailureType.CreationFailed);
        }

        _state.GuildIds.Add(entity.Id);
        _state.FavouriteGuildId ??= entity.Id;

        await directory.OnGuildChangedAsync(
            entity.ToSummarySnapshot(editorData, hasForum: false),
            ct
        );

        // The homeroom is very likely loaded — the wizard is usually opened from inside it — and
        // last resolved as an ordinary room, so the owner would have none of the rights their own
        // group just gave them. Told rather than awaited, for the reason above.
        _grainFactory
            .GetRoomGrain(RoomId.Parse(entity.RoomEntityId))
            .OnGuildChangedAsync(CancellationToken.None)
            .LogAndForget(_logger, $"refresh the homeroom of the new group {entity.Id}");

        return GuildCreationResultSnapshot.Success(
            GuildId.Parse(entity.Id),
            RoomId.Parse(entity.RoomEntityId)
        );
    }

    public async Task SetFavouriteGuildAsync(GuildId? guildId, CancellationToken ct)
    {
        // Only a group they are actually in. Clearing is always allowed.
        if (guildId is { } wanted && !_state.GuildIds.Contains(wanted.Value))
            return;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var rows = await dbCtx
                .GuildMembers.Where(x => x.PlayerEntityId == PlayerId.Value && x.IsFavourite)
                .ToListAsync(ct);

            foreach (var row in rows)
                row.IsFavourite = false;

            if (guildId is { } chosen)
            {
                var row = await dbCtx.GuildMembers.FirstOrDefaultAsync(
                    x => x.PlayerEntityId == PlayerId.Value && x.GuildEntityId == chosen.Value,
                    ct
                );

                if (row is not null)
                    row.IsFavourite = true;
            }

            await dbCtx.SaveChangesAsync(ct);
        }

        _state.FavouriteGuildId = guildId?.Value;

        var guild = guildId is { } chosenId
            ? await _grainFactory.GetGuildDirectoryGrain().GetSummaryAsync(chosenId, ct)
            : null;

        var activeRoom = await _grainFactory
            .GetPlayerPresenceGrain(PlayerId)
            .GetActiveRoomAsync(ct);

        if (activeRoom.RoomId <= 0)
            return;

        // Told, not asked, and deliberately not awaited: the room reads this grain when a player
        // walks in (RoomAvatarModule.LoadFavouriteGuildAsync), so awaiting the room from here
        // would let an entry and a badge change arrive together and deadlock the pair.
        _grainFactory
            .GetRoomGrain(activeRoom.RoomId)
            .SetPlayerFavouriteGuildAsync(
                PlayerId,
                guild?.GuildId ?? -1,
                guild is null ? -1 : (int)GuildMembershipStatus.Member,
                guild?.Name ?? string.Empty,
                CancellationToken.None
            )
            .LogAndForget(
                _logger,
                $"tell room {activeRoom.RoomId} that player {PlayerId.Value} changed group badge"
            );
    }

    public Task<int> GetMembershipCountAsync(CancellationToken ct) =>
        Task.FromResult(_state.GuildIds.Count);

    public async Task OnMembershipsChangedAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to reload guild memberships for {PlayerId}",
                PlayerId.Value
            );
        }
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var memberships = await dbCtx
            .GuildMembers.AsNoTracking()
            .Where(x =>
                x.PlayerEntityId == PlayerId.Value
                && GuildMemberRanks.MemberRanks().Contains(x.Rank)
            )
            .Select(x => new { x.GuildEntityId, x.IsFavourite })
            .ToListAsync(ct);

        _state.GuildIds.Clear();
        _state.FavouriteGuildId = null;

        foreach (var membership in memberships)
        {
            _state.GuildIds.Add(membership.GuildEntityId);

            if (membership.IsFavourite)
                _state.FavouriteGuildId = membership.GuildEntityId;
        }
    }
}
