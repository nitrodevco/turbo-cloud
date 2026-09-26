using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
using Turbo.Players.Configuration;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Subscriptions;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Players.Grains.Subscriptions;

/// <summary>
/// Owns a player's subscriptions. Write-through: an extension is saved before the in-memory row
/// moves, so an activation can be collected at any time without a flush on deactivation.
/// </summary>
internal sealed class PlayerSubscriptionGrain : Grain, IPlayerSubscriptionGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly SubscriptionConfig _subscriptionConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IPlayerSubscriptionGrain> _logger;

    private readonly PlayerSubscriptionLiveState _state;

    private PlayerId PlayerId => _state.PlayerId;

    public PlayerSubscriptionGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        ILogger<IPlayerSubscriptionGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _subscriptionConfig = playerConfig.Value.Subscriptions;
        _grainFactory = grainFactory;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to hydrate the subscriptions of player {PlayerId}",
                PlayerId
            );

            throw;
        }
    }

    public Task<PlayerSubscriptionSnapshot> GetAsync(
        SubscriptionType subscriptionType,
        CancellationToken ct
    ) => Task.FromResult(BuildSnapshot(subscriptionType));

    public Task<bool> HasActiveAsync(SubscriptionType subscriptionType, CancellationToken ct) =>
        Task.FromResult(BuildSnapshot(subscriptionType).IsActive);

    public async Task ExtendAsync(SubscriptionType subscriptionType, int days, CancellationToken ct)
    {
        if (days <= 0)
        {
            _logger.LogWarning(
                "Refused to extend the {SubscriptionType} of player {PlayerId} by {Days} days",
                subscriptionType,
                PlayerId,
                days
            );

            return;
        }

        var now = DateTime.UtcNow;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entity = await dbCtx.PlayerSubscriptions.FirstOrDefaultAsync(
            x => x.PlayerEntityId == PlayerId.Value && x.SubscriptionType == subscriptionType,
            ct
        );

        if (entity is null)
        {
            entity = new PlayerSubscriptionEntity
            {
                PlayerEntityId = PlayerId.Value,
                SubscriptionType = subscriptionType,
            };

            dbCtx.PlayerSubscriptions.Add(entity);
        }

        entity.FirstSubscribedAt ??= now;

        // A membership that is still running is extended from its end, a lapsed one from now.
        // ClubOfferSnapshot.ForSubscription shows the buyer the date this works out to, so the
        // two have to agree.
        var from = entity.ExpiresAt is { } end && end > now ? end : now;

        entity.ExpiresAt = from.AddDays(days);
        entity.TotalDaysSubscribed += days;
        entity.PeriodsPurchased++;

        if (subscriptionType == SubscriptionType.BuildersClub)
            entity.FurniLimit = NextFurniLimit(entity.FurniLimit);

        await dbCtx.SaveChangesAsync(ct);

        _state.SubscriptionsByType[subscriptionType] = entity;

        _logger.LogInformation(
            "Player {PlayerId} holds {SubscriptionType} until {ExpiresAt} after {Days} days were added",
            PlayerId,
            subscriptionType,
            entity.ExpiresAt,
            days
        );

        await SendStatusAsync(ct);

        if (subscriptionType == SubscriptionType.HabboClub)
        {
            await SendClubInfoAsync(ScrUserInfoResponseType.SubscriptionChanged, ct);

            // The room this player is standing in keeps the new expiry against their avatar for
            // the wired @is_hc variable. Not awaited: the presence has a room to tell and this
            // grain has nothing to do with what it says.
            _grainFactory
                .GetPlayerPresenceGrain(PlayerId)
                .OnHabboClubChangedAsync(entity.ExpiresAt, ct)
                .LogAndForget(
                    _logger,
                    $"tell the room of player {PlayerId} about their Habbo Club"
                );
        }

        if (subscriptionType == SubscriptionType.BuildersClub)
            // Rooms hidden while this membership was lapsed can come back. Not awaited: the
            // club has rooms of its own to talk to and this grain has nothing to do with them.
            _grainFactory
                .GetBuildersClubGrain()
                .OnSubscriptionChangedAsync(PlayerId, ct)
                .LogAndForget(_logger, $"refresh the Builders Club rooms of player {PlayerId}");
    }

    public async Task SendStatusAsync(CancellationToken ct)
    {
        var club = BuildSnapshot(SubscriptionType.HabboClub);
        var builders = BuildSnapshot(SubscriptionType.BuildersClub);

        await _grainFactory.SendComposerToPlayerAsync(
            PlayerId,
            new UserRightsMessage
            {
                ClubLevel = club.IsActive ? ClubLevelType.Vip : ClubLevelType.None,
                // Neither staff ranks nor ambassadors exist yet. They belong to the player, not
                // to a subscription, and move here when something grants them.
                SecurityLevel = SecurityLevelType.None,
                IsAmbassador = false,
            },
            ct
        );

        await _grainFactory.SendComposerToPlayerAsync(
            PlayerId,
            new BuildersClubSubscriptionStatusMessageComposer
            {
                SecondsLeft = builders.SecondsLeft,
                FurniLimit = builders.FurniLimit,
                MaxFurniLimit = builders.MaxFurniLimit,
                SecondsLeftWithGrace = builders.SecondsLeftWithGrace,
            },
            ct
        );
    }

    public async Task SendClubInfoAsync(ScrUserInfoResponseType responseType, CancellationToken ct)
    {
        var club = BuildSnapshot(SubscriptionType.HabboClub);
        var daysPerPeriod = Math.Max(1, _subscriptionConfig.DaysPerPeriod);

        await _grainFactory.SendComposerToPlayerAsync(
            PlayerId,
            new ScrSendUserInfoMessageComposer
            {
                ProductName = _subscriptionConfig.HabboClubProductName,
                // The client adds these two back up as periods * 31 + days
                // (ClubBuyCatalogWidget), so they are a split of the same total.
                DaysToPeriodEnd = club.DaysRemaining % daysPerPeriod,
                PeriodsSubscribedAhead = club.DaysRemaining / daysPerPeriod,
                MemberPeriods = club.TotalDaysSubscribed / daysPerPeriod,
                ResponseType = responseType,
                HasEverBeenMember = club.HasEverBeenMember,
                // The client gates group creation on this (HabboGroupsManager), and treats club
                // and VIP as one level, so an active membership is a VIP one.
                IsVIP = club.IsActive,
                PastClubDays = club.DaysConsumed,
                // Nothing grants citizenship VIP, which is the only thing this counts.
                PastVipDays = 0,
                MinutesUntilExpiration = club.MinutesUntilExpiration,
                MinutesSinceLastModified = club.MinutesSinceLastModified,
            },
            ct
        );
    }

    /// <summary>
    /// What a purchase raises the borrow limit to: the base on the first one, a step on each one
    /// after, never past the hotel's ceiling.
    /// </summary>
    private int NextFurniLimit(int current) =>
        Math.Min(
            _subscriptionConfig.BuildersClubMaxFurniLimit,
            current <= 0
                ? _subscriptionConfig.BuildersClubBaseFurniLimit
                : current + _subscriptionConfig.BuildersClubFurniLimitPerExtension
        );

    /// <summary>
    /// The subscription as it stands this instant. A type the player has never bought is mapped
    /// from a row that was never written, so it reads as a lapsed one everywhere.
    /// </summary>
    private PlayerSubscriptionSnapshot BuildSnapshot(SubscriptionType subscriptionType)
    {
        var entity = _state.SubscriptionsByType.GetValueOrDefault(subscriptionType);

        var snapshot = (
            entity
            ?? new PlayerSubscriptionEntity
            {
                PlayerEntityId = PlayerId.Value,
                SubscriptionType = subscriptionType,
            }
        ).ToSnapshot(
            DateTime.UtcNow,
            _subscriptionConfig.GraceDays,
            _subscriptionConfig.BuildersClubMaxFurniLimit
        );

        // Someone who has never subscribed still gets to try the Builders Club, up to the
        // handful of items the hotel lends a trial member.
        if (entity is null && subscriptionType == SubscriptionType.BuildersClub)
            return snapshot with { FurniLimit = _subscriptionConfig.BuildersClubTrialFurniLimit };

        return snapshot;
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        _state.SubscriptionsByType.Clear();

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx
            .PlayerSubscriptions.AsNoTracking()
            .Where(x => x.PlayerEntityId == PlayerId.Value)
            .ToListAsync(ct);

        foreach (var entity in entities)
            _state.SubscriptionsByType[entity.SubscriptionType] = entity;
    }
}
