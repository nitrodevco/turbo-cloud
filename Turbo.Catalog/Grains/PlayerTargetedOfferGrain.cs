using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.TargetedOffers;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Catalog.Grains;

/// <summary>
/// A player's targeted offers, write-through: a window state or a purchase is saved before it is
/// answered. The offers are configuration (<see cref="TargetedOfferConfig"/>); the grain keeps a
/// row per offer the player has seen or bought. The client asks once at login
/// (<c>OfferController.productDataReady</c> sends <c>GetNextTargetedOffer</c>), opens the dialog,
/// or docks the offer in the toolbar when its tracking state is 4, and reports each move of its
/// window with <c>SetTargetedOfferState</c>.
/// </summary>
internal sealed class PlayerTargetedOfferGrain : Grain, IPlayerTargetedOfferGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly TargetedOfferConfig _config;
    private readonly ICatalogService _catalogService;
    private readonly IGrainFactory _grainFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<IPlayerTargetedOfferGrain> _logger;

    private readonly PlayerTargetedOfferLiveState _state;

    public PlayerTargetedOfferGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<TargetedOfferConfig> config,
        ICatalogService catalogService,
        IGrainFactory grainFactory,
        TimeProvider time,
        ILogger<IPlayerTargetedOfferGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _config = config.Value;
        _catalogService = catalogService;
        _grainFactory = grainFactory;
        _time = time;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var rows = await dbCtx
            .PlayerTargetedOffers.AsNoTracking()
            .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .ToListAsync(ct);

        foreach (var row in rows)
            _state.Rows[row.OfferId] = row;
    }

    public Task SendNextOfferAsync(CancellationToken ct)
    {
        var offer = _config.Enabled
            ? _config.Offers.FirstOrDefault(x => IsRunning(x) && Left(x) > 0)
            : null;

        return offer is null
            ? SendAsync(new TargetedOfferNotFoundEventMessageComposer(), ct)
            : SendAsync(new TargetedOfferEventMessageComposer { Offer = Snapshot(offer) }, ct);
    }

    public async Task SetTrackingStateAsync(int offerId, int trackingState, CancellationToken ct)
    {
        if (!_config.Enabled || Find(offerId) is null)
            return;

        await SaveAsync(offerId, row => row.TrackingState = trackingState, ct);
    }

    public async Task<bool> PurchaseAsync(int offerId, int quantity, CancellationToken ct)
    {
        var offer = _config.Enabled ? Find(offerId) : null;

        if (offer is null || !IsRunning(offer) || quantity < 1 || quantity > Left(offer))
            return false;

        var snapshot = _catalogService.GetCatalogSnapshot(CatalogType.Normal);

        if (!snapshot.OffersById.TryGetValue(offer.CatalogOfferId, out var catalogOffer))
        {
            _logger.LogWarning(
                "Targeted offer {OfferId} grants catalogue offer {CatalogOfferId}, which does not exist",
                offer.Id,
                offer.CatalogOfferId
            );

            return false;
        }

        var costs = Costs(offer, quantity);
        var wallet = _grainFactory.GetPlayerWalletGrain(_state.PlayerId);
        var cause = MovementCause.New(
            MovementCauses.CATALOG_TARGETED_OFFER,
            _state.PlayerId.Value,
            offer.Id
        );

        if (costs.Count > 0 && !(await wallet.TryDebitAsync(costs, cause, ct)).Succeeded)
            return false;

        try
        {
            await _grainFactory
                .GetInventoryGrain(_state.PlayerId)
                .GrantCatalogOfferAsync(
                    catalogOffer,
                    string.Empty,
                    quantity,
                    cause.StartingAt(costs.Count),
                    ct
                );
        }
        catch
        {
            await _grainFactory.RefundAsync(
                _state.PlayerId,
                costs,
                cause,
                _logger,
                $"targeted offer {offer.Id}"
            );

            throw;
        }

        await SaveAsync(offerId, row => row.Purchased += quantity, ct);

        return true;
    }

    private TargetedOfferSnapshot Snapshot(TargetedOfferDefinition offer) =>
        new()
        {
            TrackingState = _state.Rows.TryGetValue(offer.Id, out var row)
                ? row.TrackingState
                : TargetedOfferTrackingStates.NEW,
            Id = offer.Id,
            Identifier = offer.Identifier,
            ProductCode = offer.ProductCode,
            PriceInCredits = offer.PriceInCredits,
            PriceInActivityPoints = offer.PriceInActivityPoints,
            ActivityPointType = offer.ActivityPointType,
            PurchaseLimit = Left(offer),
            SecondsToExpiry = offer.EndsAt is { } endsAt
                ? (int)Math.Max(1, Math.Ceiling((endsAt - Now).TotalSeconds))
                : 0,
            Title = offer.Title,
            Description = offer.Description,
            ImageUrl = offer.ImageUrl,
            IconImageUrl = offer.IconImageUrl,
            Type = offer.Type,
            SubProductCodes = [.. offer.SubProductCodes],
        };

    /// <summary>What buying that many costs: the credits and the activity points, each times the quantity.</summary>
    private static List<WalletDebitRequest> Costs(TargetedOfferDefinition offer, int quantity)
    {
        var costs = new List<WalletDebitRequest>(2);

        if (offer.PriceInCredits > 0)
            costs.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = CurrencyKind.Credits,
                    Amount = offer.PriceInCredits * quantity,
                }
            );

        if (offer.PriceInActivityPoints > 0)
            costs.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = CurrencyKind.ActivityPoints(offer.ActivityPointType),
                    Amount = offer.PriceInActivityPoints * quantity,
                }
            );

        return costs;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private bool IsRunning(TargetedOfferDefinition offer) =>
        (offer.StartsAt is null || offer.StartsAt <= Now)
        && (offer.EndsAt is null || offer.EndsAt > Now);

    /// <summary>How many the player may still buy.</summary>
    private int Left(TargetedOfferDefinition offer) =>
        offer.PurchaseLimit - (_state.Rows.TryGetValue(offer.Id, out var row) ? row.Purchased : 0);

    private TargetedOfferDefinition? Find(int offerId) =>
        _config.Offers.FirstOrDefault(x => x.Id == offerId);

    /// <summary>Changes the player's row of the offer, adding it when there is none.</summary>
    private async Task SaveAsync(
        int offerId,
        Action<PlayerTargetedOfferEntity> change,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var row = await dbCtx.PlayerTargetedOffers.FirstOrDefaultAsync(
            x => x.PlayerEntityId == _state.PlayerId.Value && x.OfferId == offerId,
            ct
        );

        if (row is null)
        {
            row = new PlayerTargetedOfferEntity
            {
                PlayerEntityId = _state.PlayerId.Value,
                OfferId = offerId,
                TrackingState = TargetedOfferTrackingStates.NEW,
                Purchased = 0,
                PlayerEntity = null!,
            };
            dbCtx.PlayerTargetedOffers.Add(row);
        }

        change(row);

        await dbCtx.SaveChangesAsync(ct);

        _state.Rows[offerId] = row;
    }

    private Task SendAsync(IComposer composer, CancellationToken ct) =>
        _grainFactory.SendComposerToPlayerAsync(_state.PlayerId, composer, ct);
}
