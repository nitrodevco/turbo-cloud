using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Catalog.Exceptions;
using Turbo.Database.Context;
using Turbo.Players.Configuration;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Catalog.Grains;

/// <summary>
/// One player's catalog purchases, keyed by the player id so they run one at a time. It holds
/// no state of its own: the wallet and the inventory it calls own what changes.
/// </summary>
internal sealed partial class CatalogPurchaseGrain : Grain, ICatalogPurchaseGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly CatalogConfig _catalogConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly IWordFilter _wordFilter;
    private readonly ICatalogService _catalogService;
    private readonly IPetBreedProvider _petBreedProvider;
    private readonly IFurnitureDefinitionProvider _definitionProvider;
    private readonly ILogger<ICatalogPurchaseGrain> _logger;

    /// <summary>Days of used-up membership that earn a club gift; never zero, so it can divide.</summary>
    private readonly int _giftIntervalDays;

    public CatalogPurchaseGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        IOptions<CatalogConfig> catalogConfig,
        IGrainFactory grainFactory,
        IWordFilter wordFilter,
        ICatalogService catalogService,
        IPetBreedProvider petBreedProvider,
        IFurnitureDefinitionProvider definitionProvider,
        ILogger<ICatalogPurchaseGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _giftIntervalDays = Math.Max(1, playerConfig.Value.Subscriptions.ClubGiftIntervalDays);
        _catalogConfig = catalogConfig.Value;
        _grainFactory = grainFactory;
        _wordFilter = wordFilter;
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

        // Hidden in the catalog editor: not shown, so not sold either, even to a client that
        // still knows its id. Its page being hidden is not the same: the club window sells the
        // memberships, and a hotel keeps them on a page of their own out of the navigator.
        if (!offer.Visible)
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

        await ValidateClubLevelAsync(offer, ct);

        ValidatePetProducts(offer, extraParam);
        extraParam = PrepareTrophyInscription(offer, extraParam);
        ValidateSubscriptionProducts(offer);
        await ValidateEffectProductsAsync(offer, quantity, ct);
        await ValidateBadgeProductsAsync(offer, ct);

        await ValidateGuildProductsAsync(offer, extraParam, ct);
        ValidateProductStuffData(offer);
        await ValidateBadgeDisplayProductsAsync(offer, extraParam, ct);

        var debitRequests = offer.ToDebitRequests(quantity);

        await DebitAsync(debitRequests, ct);

        try
        {
            await _grainFactory
                .GetInventoryGrain(this.GetPlayerId())
                .GrantCatalogOfferAsync(offer, extraParam, quantity, ct);
            await GrantBadgesAsync(offer, ct);
        }
        catch
        {
            await _grainFactory.RefundAsync(
                this.GetPlayerId(),
                debitRequests,
                _logger,
                $"catalog offer {offerId}"
            );

            throw;
        }

        // Last, because extending a membership tells the buyer it happened: a grant that threw
        // above must not leave them holding a notification for a purchase that did not land.
        await GrantSubscriptionsAsync(offer, quantity, ct);

        return offer;
    }

    /// <summary>
    /// An effect product has to name an effect the player can still be given copies of. Refused
    /// before any money moves, since the grant runs after the debit: an effect they already have
    /// for good, or copies past the cap, would otherwise be charged for and refunded. Copies of
    /// one effect across the offer's products are added up, as the grant will add them.
    /// </summary>
    private async Task ValidateEffectProductsAsync(
        CatalogOfferSnapshot offer,
        int quantity,
        CancellationToken ct
    )
    {
        var copiesByEffect = EffectProducts.CountCopies(offer.Products, quantity);

        if (copiesByEffect is null)
        {
            _logger.LogError("An effect product of offer {OfferId} names no effect", offer.Id);

            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
        }

        if (copiesByEffect.Count == 0)
            return;

        // One call for the whole offer: its effects are judged together, so a purchase that would
        // give the first and then fail on the second is refused now and not after the first is given.
        var result = await _grainFactory
            .GetPlayerEffectGrain(this.GetPlayerId())
            .CheckGiveEffectsAsync(
                [
                    .. copiesByEffect.Select(x => new EffectGrantRequest
                    {
                        EffectId = x.Key,
                        Copies = (int)Math.Min(x.Value, int.MaxValue),
                    }),
                ],
                ct
            );

        if (result != EffectGrantResult.Granted)
            throw new CatalogPurchaseException(EffectProducts.ErrorFor(result));
    }

    /// <summary>
    /// A badge product gives its badge, which a player has or has not: one they have already
    /// is refused before any money moves, with the client's own "badge owned" error.
    /// </summary>
    private async Task ValidateBadgeProductsAsync(CatalogOfferSnapshot offer, CancellationToken ct)
    {
        var codes = BadgeCodesOf(offer);

        if (codes.Count == 0)
            return;

        var badges = _grainFactory.GetPlayerBadgeGrain(this.GetPlayerId());
        var owned = await Task.WhenAll(codes.Select(code => badges.HasBadgeAsync(code, ct)));

        if (owned.Any(x => x))
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.BadgeOwned);
    }

    /// <summary>
    /// Gives the offer's badges; the badge grain tells the buyer. A badge given meanwhile by
    /// something else is had all the same, so a refusal here is not a failed purchase.
    /// </summary>
    private async Task GrantBadgesAsync(CatalogOfferSnapshot offer, CancellationToken ct)
    {
        var codes = BadgeCodesOf(offer);

        if (codes.Count == 0)
            return;

        var badges = _grainFactory.GetPlayerBadgeGrain(this.GetPlayerId());

        foreach (var code in codes)
            await badges.GiveBadgeAsync(code, ct);
    }

    private static List<string> BadgeCodesOf(CatalogOfferSnapshot offer) =>
        [
            .. offer
                .Products.Where(x =>
                    x.ProductType == ProductType.Badge && !string.IsNullOrWhiteSpace(x.ExtraParam)
                )
                .Select(x => x.ExtraParam!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];

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
            await subscriptions.ExtendPurchasedAsync(subscriptionType, granted, ct);
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

        if (
            !int.TryParse(
                extraParam,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var guildId
            )
            || guildId <= 0
        )
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);

        var rank = await _grainFactory
            .GetGuildGrain(GuildId.Parse(guildId))
            .GetMemberRankAsync(this.GetPlayerId(), ct);

        if (GuildMemberRanks.IsMember(rank))
            return;

        // Staff may buy for any group, as the client's group selector offers them at security
        // level 4; the group still has to exist.
        if (
            !await _grainFactory.HasPermissionAsync(
                this.GetPlayerId(),
                PermissionNodes.Catalog.GUILD_ANY_GROUP,
                ct
            )
            || await _grainFactory
                .GetGuildDirectoryGrain()
                .GetSummaryAsync(GuildId.Parse(guildId), ct)
                is null
        )
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
    }

    /// <summary>
    /// A paper, poster or song disc is what its product's extra parameter names, and one that
    /// names nothing (or a song disc no song) would be sold as an item that shows nothing. That
    /// is the offer's fault, not the buyer's: logged, and refused before any money moves.
    /// </summary>
    private void ValidateProductStuffData(CatalogOfferSnapshot offer)
    {
        foreach (var product in offer.Products)
        {
            var category = _definitionProvider
                .TryGetDefinition(product.FurniDefinitionId)
                ?.FurniCategory;

            if (
                category is not { } named
                || !ProductStuffData.IsNamedByProduct(named)
                || ProductStuffData.IsValid(named, product.ExtraParam)
            )
                continue;

            _logger.LogError(
                "Product {ProductId} of offer {OfferId} sells {Category} furni but its extra param {ExtraParam} names none",
                product.Id,
                offer.Id,
                named,
                product.ExtraParam
            );

            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
        }
    }

    /// <summary>
    /// A badge display shows the badge chosen on its catalog page, which travels in the extra
    /// param. Only a badge the buyer owns can be shown; anything else is refused before any
    /// money moves.
    /// </summary>
    private async Task ValidateBadgeDisplayProductsAsync(
        CatalogOfferSnapshot offer,
        string extraParam,
        CancellationToken ct
    )
    {
        var isBadgeDisplayOffer = offer.Products.Any(product =>
            BadgeDisplayData.IsBadgeDisplay(
                _definitionProvider.TryGetDefinition(product.FurniDefinitionId)?.LogicName
            )
        );

        if (!isBadgeDisplayOffer)
            return;

        var badgeCode = extraParam.Trim();

        if (
            badgeCode.Length > 0
            && await _grainFactory
                .GetPlayerBadgeGrain(this.GetPlayerId())
                .HasBadgeAsync(badgeCode, ct)
        )
            return;

        _logger.LogWarning(
            "Player {PlayerId} tried to buy badge display offer {OfferId} with badge {BadgeCode} they do not own",
            this.GetPlayerId(),
            offer.Id,
            badgeCode
        );

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
                && !int.TryParse(
                    product.ExtraParam,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out typeId
                )
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

    /// <summary>
    /// A trophy is engraved with the text typed on the trophy page, which travels in the extra
    /// param: refused when too long to engrave, before any money moves, and otherwise filtered so
    /// the inventory engraves it as it comes. Any other offer's extra param is left as sent.
    /// </summary>
    private string PrepareTrophyInscription(CatalogOfferSnapshot offer, string extraParam)
    {
        var isTrophyOffer = offer.Products.Any(product =>
            TrophyData.IsTrophy(
                _definitionProvider.TryGetDefinition(product.FurniDefinitionId)?.LogicName
            )
        );

        if (!isTrophyOffer)
            return extraParam;

        var maxLength = _catalogConfig.TrophyInscriptionMaxLength;

        if (extraParam.Trim().Length > maxLength)
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);

        return _wordFilter.FilterAndTruncate(extraParam, maxLength);
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

        await ValidateClubLevelAsync(offer, ct);

        var roomGrain = _grainFactory.GetRoomGrain(roomId);
        var controllerLevel = await roomGrain.GetControllerLevelAsync(playerId, ct);

        if (controllerLevel < RoomControllerType.Owner)
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);

        // A new promotion needs a free room; an extension needs a running one.
        var activeEvent = await roomGrain.GetActiveEventAsync(ct);

        if (extended ? activeEvent is null : activeEvent is not null)
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);

        var debitRequests = offer.ToDebitRequests(1);

        await DebitAsync(debitRequests, ct);

        RoomEventSnapshot? created = null;

        try
        {
            created = await roomGrain.CreateEventAsync(
                playerId,
                categoryId,
                name,
                description,
                duration,
                ct
            );
        }
        finally
        {
            // Covers both a refused event and a throwing room: either way nothing was bought.
            if (created is null)
            {
                _logger.LogError(
                    "Room ad offer {OfferId} was charged to player {PlayerId} but the event in room {RoomId} could not be created; refunding",
                    offerId,
                    playerId,
                    roomId
                );

                await _grainFactory.RefundAsync(
                    playerId,
                    debitRequests,
                    _logger,
                    $"room ad offer {offerId}"
                );
            }
        }

        if (created is null)
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);

        return offer;
    }

    /// <summary>
    /// A club-only offer refused before any money moves. The client greys these out for a
    /// non-member, but the level travels to it in the offer and nothing stopped a client from
    /// sending the purchase anyway.
    /// </summary>
    private async Task ValidateClubLevelAsync(CatalogOfferSnapshot offer, CancellationToken ct)
    {
        if (offer.RequiresClub && !await _grainFactory.HasActiveClubAsync(this.GetPlayerId(), ct))
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.RequiresHabboClub);
    }

    /// <summary>Takes the price, or throws the refusal the client shows for a short balance.</summary>
    private async Task DebitAsync(List<WalletDebitRequest> debitRequests, CancellationToken ct)
    {
        if (debitRequests.Count == 0)
            return;

        var result = await _grainFactory
            .GetPlayerWalletGrain(this.GetPlayerId())
            .TryDebitAsync(debitRequests, ct);

        if (!result.Succeeded)
            throw CreateInsufficientBalanceException(result);
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
