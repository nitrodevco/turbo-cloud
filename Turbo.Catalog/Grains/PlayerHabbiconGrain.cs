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
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Habbicons;
using Turbo.Primitives.Action;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Habbicons.Enums;
using Turbo.Primitives.Habbicons.Grains;
using Turbo.Primitives.Habbicons.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Messages.Outgoing.Habbicons;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Quests;

namespace Turbo.Catalog.Grains;

/// <summary>
/// A player's Habbicons, write-through: a purchase, claim, favourite or use is saved before it is
/// sent. The Habbicons are configuration (<see cref="HabbiconConfig"/>); the grain keeps a row per
/// Habbicon the player has. A collection's reward is claimable, without a row, while the player
/// owns every Habbicon of it, and owned once claimed. How the client reads the states is the AS3
/// <c>HabbiconController</c>: a buy or claim waits for <c>PurchaseOK</c> (or a purchase error)
/// before it refreshes the shop, and each change arrives as <c>UserHabbiconStatusChanged</c>.
/// </summary>
internal sealed class PlayerHabbiconGrain : Grain, IPlayerHabbiconGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly HabbiconConfig _config;
    private readonly IGrainFactory _grainFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<IPlayerHabbiconGrain> _logger;

    private readonly PlayerHabbiconLiveState _state;

    public PlayerHabbiconGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<HabbiconConfig> config,
        IGrainFactory grainFactory,
        TimeProvider time,
        ILogger<IPlayerHabbiconGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _config = config.Value;
        _grainFactory = grainFactory;
        _time = time;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var rows = await dbCtx
                .PlayerHabbicons.AsNoTracking()
                .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
                .ToListAsync(ct);

            foreach (var row in rows)
                _state.Rows[row.HabbiconId] = row;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to hydrate Habbicons for player {PlayerId}",
                _state.PlayerId
            );

            throw;
        }
    }

    public async Task SendLoginAsync(CancellationToken ct)
    {
        if (!_config.Enabled)
            return;

        await SendAsync(
            new UserHabbiconsMessageComposer
            {
                Habbicons =
                [
                    .. _state
                        .Rows.Values.OrderBy(x => x.HabbiconId)
                        .Select(x => new OwnedHabbiconSnapshot
                        {
                            HabbiconId = x.HabbiconId,
                            State = x.State,
                        }),
                ],
                RecentHabbiconIds =
                [
                    .. _state
                        .Rows.Values.Where(x => x.LastUsedAt is not null)
                        .OrderByDescending(x => x.LastUsedAt)
                        .Take(_config.MaxRecentHabbicons)
                        .Select(x => x.HabbiconId),
                ],
            },
            ct
        );
        await SendShopDataAsync(ct);
    }

    public Task SendShopDataAsync(CancellationToken ct) =>
        _config.Enabled
            ? SendAsync(
                new HabbiconShopDataMessageComposer
                {
                    Collections = [.. _config.Collections.Select(Snapshot)],
                },
                ct
            )
            : Task.CompletedTask;

    public Task SendInfoAsync(int habbiconId, CancellationToken ct)
    {
        if (!_config.Enabled || !TryFind(habbiconId, out var collection, out var habbicon))
            return Task.CompletedTask;

        return SendAsync(
            new HabbiconInfoMessageComposer
            {
                Habbicon = habbicon is not null
                    ? Snapshot(collection, habbicon)
                    : RewardSnapshot(collection),
            },
            ct
        );
    }

    public async Task BuyAsync(int habbiconId, CancellationToken ct)
    {
        if (!_config.Enabled)
            return;

        if (
            !TryFind(habbiconId, out var collection, out var habbicon)
            || habbicon is null
            || !habbicon.HasPrice
            || StateOf(collection, habbiconId) != HabbiconState.Available
        )
        {
            await SendNotAllowedAsync(ct);

            return;
        }

        await PurchaseAsync(
            collection,
            [habbicon],
            Offer(
                habbicon.Id,
                habbicon.Name,
                habbicon.PriceCredits,
                habbicon.PriceActivityPoints,
                habbicon.ActivityPointType
            ),
            ct
        );
    }

    public async Task BuyCollectionAsync(int collectionId, CancellationToken ct)
    {
        if (!_config.Enabled)
            return;

        var collection = _config.Collections.Find(x => x.Id == collectionId);

        // HabbiconView: a set can be bought while it has a price and is not complete.
        if (collection is null || !collection.HasPrice || IsCompleted(collection))
        {
            await SendNotAllowedAsync(ct);

            return;
        }

        await PurchaseAsync(
            collection,
            [.. collection.Habbicons.Where(x => !IsOwned(x.Id))],
            Offer(
                collection.Id,
                collection.Name,
                collection.PriceCredits,
                collection.PriceActivityPoints,
                collection.ActivityPointType
            ),
            ct
        );
    }

    public async Task ClaimAsync(int habbiconId, CancellationToken ct)
    {
        if (!_config.Enabled)
            return;

        var collection = _config.Collections.Find(x => x.RewardHabbiconId == habbiconId);

        if (
            collection is null
            || StateOf(collection, habbiconId) != HabbiconState.Claimable
            || !await SaveAsync([habbiconId], HabbiconState.Owned, ct)
        )
        {
            await SendNotAllowedAsync(ct);

            return;
        }

        await SendStatusAsync(habbiconId, ct);
        await SendAsync(
            new PurchaseOKMessageComposer
            {
                Offer = Offer(habbiconId, collection.RewardHabbiconName, 0, 0, 0),
            },
            ct
        );
    }

    public async Task<bool> GiveAsync(int habbiconId, CancellationToken ct)
    {
        if (!_config.Enabled || !TryFind(habbiconId, out var collection, out _))
            return false;

        if (IsOwned(habbiconId))
            return true;

        var completedBefore = IsCompleted(collection);

        if (!await SaveAsync([habbiconId], HabbiconState.Owned, ct))
            return false;

        await SendStatusAsync(habbiconId, ct);

        if (!completedBefore && IsCompleted(collection) && collection.RewardHabbiconId > 0)
            await SendStatusAsync(collection.RewardHabbiconId, ct);

        return true;
    }

    public async Task SetFavouriteAsync(int habbiconId, bool favourite, CancellationToken ct)
    {
        if (!_config.Enabled || !IsOwned(habbiconId))
            return;

        var state = favourite ? HabbiconState.Favourite : HabbiconState.Owned;

        if (_state.Rows[habbiconId].State == state || !await SaveAsync([habbiconId], state, ct))
            return;

        await SendStatusAsync(habbiconId, ct);
    }

    public async Task<bool> TriggerAsync(ActionContext ctx, int habbiconId, CancellationToken ct)
    {
        if (!_config.Enabled || !IsOwned(habbiconId) || ctx.RoomId <= 0)
            return false;

        if (!await _grainFactory.GetRoomGrain(ctx.RoomId).ShowHabbiconAsync(ctx, habbiconId, ct))
            return false;

        await NoteUsedAsync(habbiconId, ct);

        _grainFactory
            .GetPlayerRewardTrackGrain(_state.PlayerId)
            .RecordActionAsync(RewardTrackActionTypes.USE_HABBICON, string.Empty, ct)
            .LogAndForget(_logger, "count a Habbicon use for player {PlayerId}", _state.PlayerId);

        return true;
    }

    public Task<bool> CanUseAsync(int habbiconId, CancellationToken ct) =>
        Task.FromResult(_config.Enabled && IsOwned(habbiconId));

    public async Task NoteUsedAsync(int habbiconId, CancellationToken ct)
    {
        if (!IsOwned(habbiconId))
            return;

        var usedAt = _time.GetUtcNow().UtcDateTime;

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            await dbCtx
                .PlayerHabbicons.Where(x =>
                    x.PlayerEntityId == _state.PlayerId.Value && x.HabbiconId == habbiconId
                )
                .ExecuteUpdateAsync(x => x.SetProperty(r => r.LastUsedAt, usedAt), ct);

            _state.Rows[habbiconId].LastUsedAt = usedAt;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to note Habbicon {HabbiconId} used by player {PlayerId}",
                habbiconId,
                _state.PlayerId
            );
        }
    }

    /// <summary>
    /// Takes the price, gives the Habbicons, and tells the client: each new state, the reward
    /// when they complete the collection, then <c>PurchaseOK</c>. A short balance is the
    /// catalog's purchase error, which the Habbicon window also answers to.
    /// </summary>
    private async Task PurchaseAsync(
        HabbiconCollectionDefinition collection,
        List<HabbiconDefinition> habbicons,
        CatalogOfferSnapshot offer,
        CancellationToken ct
    )
    {
        var costs = offer.ToDebitRequests(1);
        var wallet = _grainFactory.GetPlayerWalletGrain(_state.PlayerId);

        if (costs.Count > 0)
        {
            var debit = await wallet.TryDebitAsync(costs, ct);

            if (!debit.Succeeded)
            {
                await SendAsync(
                    new PurchaseErrorMessageComposer
                    {
                        ErrorCode =
                            debit.Failure?.CurrencyKind.CurrencyType == CurrencyType.Credits
                                ? CatalogPurchaseErrorType.NotEnoughCredits
                                : CatalogPurchaseErrorType.NotEnoughActivityPoints,
                    },
                    ct
                );

                return;
            }
        }

        var completedBefore = IsCompleted(collection);

        if (!await SaveAsync([.. habbicons.Select(x => x.Id)], HabbiconState.Owned, ct))
        {
            // Nothing was given, so the price goes back.
            foreach (var cost in costs)
                await wallet.CreditAsync(cost.CurrencyKind, cost.Amount, ct);

            await SendNotAllowedAsync(ct);

            return;
        }

        foreach (var habbicon in habbicons)
            await SendStatusAsync(habbicon.Id, ct);

        if (!completedBefore && IsCompleted(collection) && collection.RewardHabbiconId > 0)
            await SendStatusAsync(collection.RewardHabbiconId, ct);

        await SendAsync(new PurchaseOKMessageComposer { Offer = offer }, ct);
    }

    /// <summary>Writes the Habbicons in that state, adding rows where the player has none.</summary>
    private async Task<bool> SaveAsync(
        IReadOnlyCollection<int> habbiconIds,
        HabbiconState state,
        CancellationToken ct
    )
    {
        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var rows = await dbCtx
                .PlayerHabbicons.Where(x =>
                    x.PlayerEntityId == _state.PlayerId.Value && habbiconIds.Contains(x.HabbiconId)
                )
                .ToDictionaryAsync(x => x.HabbiconId, ct);

            foreach (var habbiconId in habbiconIds)
            {
                if (rows.TryGetValue(habbiconId, out var row))
                {
                    row.State = state;

                    continue;
                }

                row = new PlayerHabbiconEntity
                {
                    PlayerEntityId = _state.PlayerId.Value,
                    HabbiconId = habbiconId,
                    State = state,
                    PlayerEntity = null!,
                };
                dbCtx.PlayerHabbicons.Add(row);
                rows[habbiconId] = row;
            }

            await dbCtx.SaveChangesAsync(ct);

            foreach (var row in rows.Values)
                _state.Rows[row.HabbiconId] = row;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save Habbicons {HabbiconIds} as {State} for player {PlayerId}",
                habbiconIds,
                state,
                _state.PlayerId
            );

            return false;
        }
    }

    private Task SendStatusAsync(int habbiconId, CancellationToken ct) =>
        TryFind(habbiconId, out var collection, out _)
            ? SendAsync(
                new UserHabbiconStatusChangedMessageComposer
                {
                    HabbiconId = habbiconId,
                    State = StateOf(collection, habbiconId),
                },
                ct
            )
            : Task.CompletedTask;

    /// <summary>A refusal the Habbicon window answers to (<c>HabbiconController.onPurchaseFailed</c>).</summary>
    private Task SendNotAllowedAsync(CancellationToken ct) =>
        SendAsync(
            new PurchaseNotAllowedMessageComposer
            {
                ErrorType = CatalogPurchaseErrorType.OfferNotFound,
            },
            ct
        );

    private Task SendAsync(IComposer composer, CancellationToken ct) =>
        _grainFactory.SendComposerToPlayerAsync(_state.PlayerId, composer, ct);

    private bool IsOwned(int habbiconId) =>
        _state.Rows.TryGetValue(habbiconId, out var row)
        && row.State is HabbiconState.Owned or HabbiconState.Favourite;

    private bool IsCompleted(HabbiconCollectionDefinition collection) =>
        collection.Habbicons.Count > 0 && collection.Habbicons.All(x => IsOwned(x.Id));

    /// <summary>The player's state of a Habbicon of <paramref name="collection"/>, or of its reward.</summary>
    private HabbiconState StateOf(HabbiconCollectionDefinition collection, int habbiconId)
    {
        if (_state.Rows.TryGetValue(habbiconId, out var row))
            return row.State;

        if (habbiconId == collection.RewardHabbiconId)
            return IsCompleted(collection) ? HabbiconState.Claimable : HabbiconState.Locked;

        return collection.Habbicons.Find(x => x.Id == habbiconId) is { HasPrice: true }
            ? HabbiconState.Available
            : HabbiconState.Reward;
    }

    /// <summary>The collection holding the Habbicon; <paramref name="habbicon"/> is null for its reward.</summary>
    private bool TryFind(
        int habbiconId,
        out HabbiconCollectionDefinition collection,
        out HabbiconDefinition? habbicon
    )
    {
        foreach (var candidate in _config.Collections)
        {
            habbicon = candidate.Habbicons.Find(x => x.Id == habbiconId);

            if (habbicon is not null || candidate.RewardHabbiconId == habbiconId)
            {
                collection = candidate;

                return true;
            }
        }

        collection = null!;
        habbicon = null;

        return false;
    }

    private HabbiconCollectionSnapshot Snapshot(HabbiconCollectionDefinition collection) =>
        new()
        {
            CollectionId = collection.Id,
            Name = collection.Name,
            Completed = IsCompleted(collection),
            RewardHabbiconId = collection.RewardHabbiconId,
            RewardState =
                collection.RewardHabbiconId > 0
                    ? StateOf(collection, collection.RewardHabbiconId)
                    : HabbiconState.Locked,
            PriceCredits = collection.PriceCredits,
            PriceActivityPoints = collection.PriceActivityPoints,
            ActivityPointType = collection.ActivityPointType,
            Habbicons = [.. collection.Habbicons.Select(x => Snapshot(collection, x))],
        };

    private HabbiconSnapshot Snapshot(
        HabbiconCollectionDefinition collection,
        HabbiconDefinition habbicon
    ) =>
        new()
        {
            HabbiconId = habbicon.Id,
            Name = habbicon.Name,
            CollectionId = collection.Id,
            State = StateOf(collection, habbicon.Id),
            PriceCredits = habbicon.PriceCredits,
            PriceActivityPoints = habbicon.PriceActivityPoints,
            ActivityPointType = habbicon.ActivityPointType,
        };

    private HabbiconSnapshot RewardSnapshot(HabbiconCollectionDefinition collection) =>
        new()
        {
            HabbiconId = collection.RewardHabbiconId,
            Name = collection.RewardHabbiconName,
            CollectionId = collection.Id,
            State = StateOf(collection, collection.RewardHabbiconId),
            PriceCredits = 0,
            PriceActivityPoints = 0,
            ActivityPointType = collection.ActivityPointType,
        };

    /// <summary>
    /// What <c>PurchaseOK</c> carries for a Habbicon buy: no catalog offer is involved, so the
    /// Habbicon (or set) stands in for one, with its price and no products.
    /// </summary>
    private static CatalogOfferSnapshot Offer(
        int id,
        string name,
        int priceCredits,
        int priceActivityPoints,
        int activityPointType
    ) =>
        new()
        {
            Id = id,
            PageId = -1,
            LocalizationId = $"habbicon_{name}",
            Rentable = false,
            CostCredits = priceCredits,
            CostSilver = 0,
            CostCurrency = priceActivityPoints,
            ActivityPointType = priceActivityPoints > 0 ? activityPointType : null,
            CanGift = false,
            CanBundle = false,
            ClubLevel = 0,
            Visible = true,
            ProductIds = [],
            Products = [],
        };
}
