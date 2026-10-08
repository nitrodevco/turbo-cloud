using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Catalog.Exceptions;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Catalog.Grains;

internal sealed partial class CatalogPurchaseGrain
{
    /// <summary>
    /// Buys an offer for someone else. Everything that can refuse it (the offer, the wrapping,
    /// the note, the receiver) is checked before any money moves; the receiver's inventory then
    /// wraps it, and a failure there refunds the buyer. A gift holds one piece of furni, because
    /// a present opens into one item.
    /// </summary>
    public async Task<CatalogOfferSnapshot> PurchaseOfferAsGiftAsync(
        CatalogGiftRequest request,
        CancellationToken ct
    )
    {
        var buyerId = this.GetPlayerId();
        var wrapping = _giftWrappingProvider.GetWrapping();

        if (!wrapping.Enabled)
        {
            _logger.LogWarning(
                "Player {PlayerId} tried to buy offer {OfferId} as a gift while gifting is off",
                buyerId,
                request.OfferId
            );

            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
        }

        var snapshot = _catalogService.GetCatalogSnapshot(CatalogType.Normal);

        if (
            !snapshot.OffersById.TryGetValue(request.OfferId, out var offer)
            || !offer.Visible
            || offer.ClubGiftDaysRequired is not null
        )
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.OfferNotFound);

        var (product, content) = GetGiftableProduct(offer, buyerId);
        var paidWrapping = IsPaidWrapping(request, wrapping, buyerId);
        var presentDefinitionId = GetPresentDefinitionId(request.SpriteId, buyerId);
        var message = PrepareGiftMessage(request.Message);

        await ValidateClubLevelAsync(offer, ct);

        var extraParam = PrepareTrophyInscription(offer, request.ExtraParam);

        ValidateProductStuffData(offer);

        var receiverId = await GetGiftReceiverAsync(request.ReceiverName, buyerId, ct);

        var buyer = await _grainFactory.GetPlayerGrain(buyerId).GetSummaryAsync(ct);

        var debitRequests = offer.ToDebitRequests(1);

        if (paidWrapping)
            AddWrappingPrice(debitRequests, wrapping.Price);

        await DebitAsync(debitRequests, ct);

        try
        {
            await _grainFactory
                .GetInventoryGrain(receiverId)
                .ReceivePresentAsync(
                    new PresentGrantRequest
                    {
                        Product = product,
                        ExtraParam = extraParam,
                        PresentDefinitionId = presentDefinitionId,
                        BoxType = paidWrapping ? request.BoxType : 0,
                        RibbonType = paidWrapping ? request.RibbonType : 0,
                        Message = message,
                        PurchaserName = request.ShowPurchaserName ? buyer.Name : null,
                        PurchaserFigure = request.ShowPurchaserName ? buyer.Figure : null,
                        BuyerName = buyer.Name,
                    },
                    ct
                );
        }
        catch
        {
            await _grainFactory.RefundAsync(
                buyerId,
                debitRequests,
                _logger,
                $"gift of catalog offer {offer.Id}"
            );

            throw;
        }

        _logger.LogInformation(
            "Player {PlayerId} gave offer {OfferId} ({ProductCode}) to player {ReceiverId}",
            buyerId,
            offer.Id,
            content.Name,
            receiverId
        );

        return offer;
    }

    /// <summary>
    /// The one piece of furni a giftable offer holds. An offer whose <c>can_gift</c> is off is
    /// refused outright; otherwise anything that cannot sit in a present on
    /// its own is refused: several items, a teleporter (a pair), a limited edition (raffled),
    /// and furni that belongs to its buyer (guild furni for their group, a badge display of
    /// their badge).
    /// </summary>
    private (
        CatalogProductSnapshot Product,
        FurnitureDefinitionSnapshot Definition
    ) GetGiftableProduct(CatalogOfferSnapshot offer, PlayerId buyerId)
    {
        // The offer's own can_gift comes first: the client hides the gift button on such an
        // offer, so a gift of one is a client that sent the purchase anyway.
        if (!offer.CanGift)
        {
            _logger.LogWarning(
                "Player {PlayerId} tried to gift offer {OfferId}, which is not giftable (can_gift off)",
                buyerId,
                offer.Id
            );

            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
        }

        var product = offer.Products.Length == 1 ? offer.Products[0] : null;
        var definition = product is not null
            ? _definitionProvider.TryGetDefinition(product.FurniDefinitionId)
            : null;

        if (
            product is null
            || product.ProductType is not (ProductType.Floor or ProductType.Wall)
            || product.Quantity != 1
            || product.UniqueSize > 0
            || definition is null
            || TeleportFurniture.IsLinkedPair(definition.LogicName, definition.Name)
            || GuildFurnitureLogicNames.IsGuildFurniture(definition.LogicName)
            || BadgeDisplayData.IsBadgeDisplay(definition.LogicName)
        )
        {
            _logger.LogWarning(
                "Player {PlayerId} tried to gift offer {OfferId}, which does not hold one piece of furni a present can carry",
                buyerId,
                offer.Id
            );

            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
        }

        return (product, definition);
    }

    /// <summary>
    /// Whether the buyer chose a paid wrapping. The free box is one of the default presents with
    /// no style; a paid one is a configured colour, box style and ribbon. Anything else is a
    /// wrapping the dialog never offered.
    /// </summary>
    private bool IsPaidWrapping(
        CatalogGiftRequest request,
        GiftWrappingSnapshot wrapping,
        PlayerId buyerId
    )
    {
        if (
            wrapping.DefaultStuffTypes.Contains(request.SpriteId)
            && request.BoxType == 0
            && request.RibbonType == 0
        )
            return false;

        if (
            wrapping.StuffTypes.Contains(request.SpriteId)
            && wrapping.BoxTypes.Contains(request.BoxType)
            && wrapping.RibbonTypes.Contains(request.RibbonType)
        )
            return true;

        _logger.LogWarning(
            "Player {PlayerId} chose gift wrapping {SpriteId} box {BoxType} ribbon {RibbonType}, which is not offered",
            buyerId,
            request.SpriteId,
            request.BoxType,
            request.RibbonType
        );

        throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
    }

    /// <summary>The furniture definition of the present the client named by sprite.</summary>
    private int GetPresentDefinitionId(int spriteId, PlayerId buyerId)
    {
        var definition = _definitionProvider.TryGetDefinitionBySprite(ProductType.Floor, spriteId);

        if (definition is not null && PresentData.IsPresent(definition.LogicName))
            return definition.Id;

        _logger.LogError(
            "Gift wrapping sprite {SpriteId} chosen by player {PlayerId} is not a present definition",
            spriteId,
            buyerId
        );

        throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
    }

    /// <summary>The note on the tag: refused when too long to fit, otherwise filtered.</summary>
    private string PrepareGiftMessage(string message)
    {
        var trimmed = message.Trim();

        if (trimmed.Length > _catalogConfig.GiftWrapping.MessageMaxLength)
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.InvalidGiftMessage);

        return _wordFilter.Filter(trimmed);
    }

    /// <summary>
    /// The player a gift is for. A name nobody has is the dialog's "user not found"; a receiver
    /// who has blocked the buyer refuses it.
    /// </summary>
    private async Task<PlayerId> GetGiftReceiverAsync(
        string receiverName,
        PlayerId buyerId,
        CancellationToken ct
    )
    {
        var name = receiverName.Trim();
        var receiverId =
            name.Length > 0
                ? await _grainFactory.GetPlayerDirectoryGrain().GetPlayerIdAsync(name, ct)
                : null;

        if (receiverId is not { } found)
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.ReceiverNotFound);

        if (
            found != buyerId
            && await _grainFactory.GetPlayerMessengerGrain(found).IsBlockingAsync(buyerId, ct)
        )
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.BlockedByReceiver);

        return found;
    }

    /// <summary>A paid wrapping costs credits on top of whatever the offer costs.</summary>
    private static void AddWrappingPrice(List<WalletDebitRequest> debitRequests, int price)
    {
        if (price <= 0)
            return;

        var index = debitRequests.FindIndex(x => x.CurrencyKind == CurrencyKind.Credits);

        if (index < 0)
        {
            debitRequests.Add(
                new WalletDebitRequest { CurrencyKind = CurrencyKind.Credits, Amount = price }
            );

            return;
        }

        debitRequests[index] = debitRequests[index] with
        {
            Amount = debitRequests[index].Amount + price,
        };
    }
}
