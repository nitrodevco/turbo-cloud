using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Exceptions;
using Turbo.Database.Context;
using Turbo.Players.Configuration;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Catalog.Grains;

/// <summary>
/// One player's catalog purchases, keyed by the player id so they run one at a time. It holds
/// no state of its own: the wallet and the inventory it calls own what changes.
/// </summary>
internal sealed partial class CatalogPurchaseGrain : Grain, ICatalogPurchaseGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly IGrainFactory _grainFactory;
    private readonly ICatalogService _catalogService;
    private readonly IPetBreedProvider _petBreedProvider;
    private readonly IFurnitureDefinitionProvider _definitionProvider;
    private readonly ILogger<ICatalogPurchaseGrain> _logger;

    /// <summary>Days of used-up membership that earn a club gift; never zero, so it can divide.</summary>
    private readonly int _giftIntervalDays;

    public CatalogPurchaseGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        ICatalogService catalogService,
        IPetBreedProvider petBreedProvider,
        IFurnitureDefinitionProvider definitionProvider,
        ILogger<ICatalogPurchaseGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _giftIntervalDays = Math.Max(1, playerConfig.Value.Subscriptions.ClubGiftIntervalDays);
        _grainFactory = grainFactory;
        _catalogService = catalogService;
        _petBreedProvider = petBreedProvider;
        _definitionProvider = definitionProvider;
        _logger = logger;
    }

    public async Task<CatalogOfferSnapshot> PurchaseOfferFromCatalogAsync(
        CatalogType catalogType,
        int offerId,
        string extraParam,
        int quantity,
        CancellationToken ct
    )
    {
        quantity = Math.Max(1, quantity);
        // TODO validate quantity

        var snapshot = _catalogService.GetCatalogSnapshot(catalogType);

        if (!snapshot.OffersById.TryGetValue(offerId, out var offer))
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.OfferNotFound);

        // A club gift sits in the normal catalog like any other offer and is priced at nothing,
        // because it is earned rather than sold. Buying one is not a purchase this shop makes:
        // it goes through ClaimClubGiftAsync, which spends a gift the member has earned.
        if (offer.ClubGiftDaysRequired is not null)
        {
            _logger.LogWarning(
                "Player {PlayerId} tried to buy club gift offer {OfferId} instead of claiming it",
                this.GetPlayerId(),
                offerId
            );

            throw new CatalogPurchaseException(CatalogPurchaseErrorType.OfferNotFound);
        }

        ValidatePetProducts(offer, extraParam);
        ValidateSubscriptionProducts(offer);

        await ValidateGuildProductsAsync(offer, extraParam, ct);

        if (TryGetDebitRequests(offer, quantity, out var debitRequests))
        {
            var result = await _grainFactory
                .GetPlayerWalletGrain(this.GetPlayerId().Value)
                .TryDebitAsync(debitRequests, ct);

            if (!result.Succeeded)
                throw CreateInsufficientBalanceException(result);
        }

        await _grainFactory
            .GetInventoryGrain(this.GetPlayerId().Value)
            .GrantCatalogOfferAsync(offer, extraParam, quantity, ct);

        // Last, because extending a membership tells the buyer it happened: a grant that threw
        // above must not leave them holding a notification for a purchase that did not land.
        await GrantSubscriptionsAsync(offer, quantity, ct);

        return offer;
    }

    /// <summary>
    /// A product that sells membership has to say how much of it. Refuse the offer before any
    /// money moves, rather than charging for days that were never configured.
    /// </summary>
    private void ValidateSubscriptionProducts(CatalogOfferSnapshot offer)
    {
        foreach (var product in offer.Products)
        {
            if (product.SubscriptionType is not { } subscriptionType)
                continue;

            if (product.SubscriptionDays > 0)
                continue;

            _logger.LogError(
                "Product {ProductId} of offer {OfferId} grants {SubscriptionType} but no days",
                product.Id,
                offer.Id,
                subscriptionType
            );

            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
        }
    }

    /// <summary>
    /// Hands the subscription days in an offer to the player's subscriptions. A membership is
    /// not inventory, so it does not go through <c>InventoryGrain</c>, which ignores the
    /// products it does not know. Every product here was checked by
    /// <see cref="ValidateSubscriptionProducts"/> before the buyer was charged.
    /// </summary>
    private async Task GrantSubscriptionsAsync(
        CatalogOfferSnapshot offer,
        int quantity,
        CancellationToken ct
    )
    {
        var days = new Dictionary<SubscriptionType, int>();

        foreach (var product in offer.Products)
        {
            if (product.SubscriptionType is not { } subscriptionType)
                continue;

            days[subscriptionType] =
                days.GetValueOrDefault(subscriptionType) + product.SubscriptionDays * quantity;
        }

        var subscriptions = _grainFactory.GetPlayerSubscriptionGrain(this.GetPlayerId());

        // One call per type, not per product, so a buyer is told once about each membership.
        foreach (var (subscriptionType, granted) in days)
            await subscriptions.ExtendAsync(subscriptionType, granted, ct);
    }

    /// <summary>
    /// Guild furni is bought for a group, and the group travels in the purchase's extra param.
    /// The catalog page only offers groups the buyer is in, so this is the check against a
    /// client that sent one it was never shown — without it, anyone could wear any group's badge
    /// on their furni.
    /// </summary>
    private async Task ValidateGuildProductsAsync(
        CatalogOfferSnapshot offer,
        string extraParam,
        CancellationToken ct
    )
    {
        var isGuildOffer = offer.Products.Any(product =>
            GuildFurnitureLogicNames.IsGuildFurniture(
                _definitionProvider.TryGetDefinition(product.FurniDefinitionId)?.LogicName
            )
        );

        if (!isGuildOffer)
            return;

        if (!int.TryParse(extraParam, out var guildId) || guildId <= 0)
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);

        var rank = await _grainFactory
            .GetGuildGrain(GuildId.Parse(guildId))
            .GetMemberRankAsync(this.GetPlayerId(), ct);

        if (rank is not (GuildMemberRank.Owner or GuildMemberRank.Admin or GuildMemberRank.Member))
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
    }

    /// <summary>
    /// A pet purchase carries the name, breed and colour the buyer chose; refuse it before any
    /// money moves when it cannot be granted, since the grant runs after the debit.
    /// </summary>
    private void ValidatePetProducts(CatalogOfferSnapshot offer, string extraParam)
    {
        foreach (var product in offer.Products)
        {
            if (product.ProductType != ProductType.Pet)
                continue;

            if (
                !PetProductCodes.TryGetTypeId(product.ClassName, out var typeId)
                && !int.TryParse(product.ExtraParam, out typeId)
            )
            {
                _logger.LogError(
                    "Pet product {ProductId} of offer {OfferId} names no pet type",
                    product.Id,
                    offer.Id
                );

                throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
            }

            if (!PetPurchaseData.TryParse(extraParam, out var purchase))
                throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);

            var palette = _petBreedProvider.TryGetPalette(typeId, purchase.PaletteId);

            if (palette is null || !palette.Sellable)
                throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
        }
    }

    public async Task<CatalogOfferSnapshot> PurchaseRoomAdAsync(
        int offerId,
        RoomId roomId,
        int categoryId,
        string name,
        string description,
        bool extended,
        TimeSpan duration,
        CancellationToken ct
    )
    {
        var playerId = this.GetPlayerId();
        var snapshot = _catalogService.GetCatalogSnapshot(CatalogType.Normal);

        if (!snapshot.OffersById.TryGetValue(offerId, out var offer))
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.OfferNotFound);

        var roomGrain = _grainFactory.GetRoomGrain(roomId);
        var controllerLevel = await roomGrain.GetControllerLevelAsync(playerId, ct);

        if (controllerLevel < RoomControllerType.Owner)
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);

        // A new promotion needs a free room; an extension needs a running one.
        var activeEvent = await roomGrain.GetActiveEventAsync(ct);

        if (extended ? activeEvent is null : activeEvent is not null)
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);

        if (TryGetDebitRequests(offer, 1, out var debitRequests))
        {
            var result = await _grainFactory
                .GetPlayerWalletGrain(playerId)
                .TryDebitAsync(debitRequests, ct);

            if (!result.Succeeded)
                throw CreateInsufficientBalanceException(result);
        }

        var created = await roomGrain.CreateEventAsync(
            playerId,
            categoryId,
            name,
            description,
            duration,
            ct
        );

        if (created is null)
        {
            _logger.LogError(
                "Room ad offer {OfferId} was charged to player {PlayerId} but the event in room {RoomId} could not be created",
                offerId,
                playerId,
                roomId
            );

            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
        }

        return offer;
    }

    private bool TryGetDebitRequests(
        CatalogOfferSnapshot offer,
        int quantity,
        out List<WalletDebitRequest> requests
    )
    {
        requests = [];

        if (offer.CostCredits > 0)
            requests.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = new CurrencyKind { CurrencyType = CurrencyType.Credits },
                    Amount = offer.CostCredits * quantity,
                }
            );

        if (offer.CostSilver > 0)
            requests.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = new CurrencyKind { CurrencyType = CurrencyType.Silver },
                    Amount = offer.CostSilver * quantity,
                }
            );

        if (offer.CostCurrency > 0)
            requests.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = new CurrencyKind
                    {
                        CurrencyType = CurrencyType.ActivityPoints,
                        ActivityPointType = offer.CurrencyTypeId,
                    },
                    Amount = offer.CostCurrency * quantity,
                }
            );

        return true;
    }

    private static CatalogPurchaseException CreateInsufficientBalanceException(
        WalletDebitResult debitResult
    )
    {
        if (debitResult.Failure is null)
            return new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);

        if (debitResult.Failure.CurrencyKind.CurrencyType == CurrencyType.Credits)
        {
            return new CatalogPurchaseException(
                CatalogPurchaseErrorType.NotEnoughCredits,
                new CatalogBalanceFailure
                {
                    NotEnoughCredits = true,
                    NotEnoughActivityPoints = false,
                    ActivityPointType = 0,
                }
            );
        }

        if (debitResult.Failure.CurrencyKind.CurrencyType == CurrencyType.ActivityPoints)
        {
            return new CatalogPurchaseException(
                CatalogPurchaseErrorType.NotEnoughActivityPoints,
                new CatalogBalanceFailure
                {
                    NotEnoughCredits = false,
                    NotEnoughActivityPoints = true,
                    ActivityPointType = debitResult.Failure.CurrencyKind.ActivityPointType ?? -1,
                }
            );
        }

        return new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
    }
}
