using System;
using System.Collections.Immutable;
using System.Globalization;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;

namespace Turbo.Database.Extensions;

/// <summary>
/// Catalog rows to snapshots. The catalog is a tree, so each row takes the ids (and children)
/// the provider has already grouped for it.
/// </summary>
public static class CatalogEntityExtensions
{
    public static CatalogPageSnapshot ToSnapshot(
        this CatalogPageEntity entity,
        ImmutableArray<int> offerIds,
        ImmutableArray<int> childIds
    ) =>
        new()
        {
            Id = entity.Id,
            ParentId = entity.ParentEntityId ?? -1,
            Localization = entity.Localization,
            Name = entity.Name,
            Icon = entity.Icon,
            Layout = entity.Layout,
            ImageData = entity.ImageData ?? [],
            TextData = entity.TextData ?? [],
            // An invisible page is still sent, hidden, so a link that opens it by name finds it.
            Visible = entity.Display != CatalogPageDisplay.Invisible,
            OfferIds = offerIds,
            ChildIds = childIds,
        };

    /// <param name="activityPointType">
    /// The activity-point type of the offer's currency row, which the provider looks up; the row's
    /// id is not the type.
    /// </param>
    public static CatalogOfferSnapshot ToSnapshot(
        this CatalogOfferEntity entity,
        ImmutableArray<int> productIds,
        ImmutableArray<CatalogProductSnapshot> products,
        int? activityPointType
    ) =>
        new()
        {
            Id = entity.Id,
            PageId = entity.CatalogPageEntityId,
            LocalizationId = entity.LocalizationId ?? string.Empty,
            Rentable = false,
            CostCredits = entity.CostCredits,
            CostSilver = 0,
            CostCurrency = entity.CostCurrency,
            ActivityPointType = activityPointType,
            CanGift = entity.CanGift,
            CanBundle = entity.CanBundle,
            ClubLevel = entity.ClubLevel,
            Visible = entity.Visible,
            ProductIds = productIds,
            Products = products,
            ClubGiftDaysRequired = entity.ClubGiftDaysRequired,
        };

    /// <param name="definition">The furniture the product grants; null for non-furni products.</param>
    /// <param name="series">The limited series the product is sold from, when it has one.</param>
    public static CatalogProductSnapshot ToSnapshot(
        this CatalogProductEntity entity,
        FurnitureDefinitionSnapshot? definition,
        LtdSeriesEntity? series
    ) =>
        new()
        {
            Id = entity.Id,
            OfferId = entity.CatalogOfferEntityId,
            ProductType = entity.ProductType,
            FurniDefinitionId = entity.FurnitureDefinitionEntityId ?? -1,
            // An effect has no definition; the client reads its effect id where a sprite id goes.
            SpriteId =
                entity.ProductType == ProductType.Effect
                && EffectProducts.TryGetEffectId(entity.ExtraParam, out var effectId)
                    ? effectId
                    : definition?.SpriteId ?? -1,
            ExtraParam = entity.ExtraParam,
            Quantity = entity.Quantity,
            UniqueSize = series?.TotalQuantity ?? 0,
            UniqueRemaining = series?.RemainingQuantity ?? 0,
            LtdSeriesId = series?.Id,
            ClassName = definition?.Name,
            SubscriptionType = entity.SubscriptionType,
            SubscriptionDays = entity.SubscriptionDays,
        };

    /// <summary>
    /// A featured item as the front page draws it. Its countdown is left at none: it is worked
    /// out from <see cref="CatalogFrontPageItemSnapshot.ExpiresAt"/> when a page is sent.
    /// </summary>
    public static CatalogFrontPageItemSnapshot ToSnapshot(this CatalogFeaturedItemEntity entity) =>
        new()
        {
            Position = entity.Position,
            ItemName = entity.Title,
            ItemPromoImage = entity.Image,
            Type = entity.Type,
            CatalogPageLocation =
                entity.Type == CatalogFrontPageItemType.Page ? entity.Value : null,
            ProductOfferId =
                entity.Type == CatalogFrontPageItemType.Offer
                && int.TryParse(
                    entity.Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var offerId
                )
                    ? offerId
                    : null,
            ProductCode = entity.Type == CatalogFrontPageItemType.Product ? entity.Value : null,
            ExpiresInSeconds = 0,
            ExpiresAt = entity.ExpiresAt,
        };

    public static LtdSeriesSnapshot ToSnapshot(this LtdSeriesEntity entity) =>
        new()
        {
            Id = entity.Id,
            CatalogProductId = entity.CatalogProductEntityId,
            TotalQuantity = entity.TotalQuantity,
            RemainingQuantity = entity.RemainingQuantity,
            RaffleWindowSeconds = entity.RaffleWindowSeconds,
            IsActive = entity.IsActive,
            IsRaffleFinished = entity.IsRaffleFinished,
            StartsAt = entity.StartsAt,
            EndsAt = entity.EndsAt,
        };

    public static VoucherSnapshot ToSnapshot(this VoucherEntity entity) =>
        new()
        {
            Id = entity.Id,
            Code = entity.Code,
            Credits = entity.Credits,
            CurrencyTypeId = entity.CurrencyTypeEntityId,
            CurrencyAmount = entity.CurrencyAmount,
            FurnitureDefinitionId = entity.FurnitureDefinitionEntityId,
            FurnitureQuantity = entity.FurnitureQuantity,
            BadgeCode = entity.BadgeCode,
            MaxUses = entity.MaxUses,
            Uses = entity.Uses,
            ExpiresAt = entity.ExpiresAt is { } expires
                ? DateTime.SpecifyKind(expires, DateTimeKind.Utc)
                : null,
            Enabled = entity.Enabled,
            Note = entity.Note,
            CreatedAt = DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc),
        };

    public static CatalogPageExpirySnapshot ToSnapshot(
        this CatalogPageExpiryEntity entity,
        string pageName
    ) =>
        new()
        {
            Id = entity.Id,
            PageId = entity.CatalogPageEntityId,
            PageName = pageName,
            ExpiresAt = DateTime.SpecifyKind(entity.ExpiresAt, DateTimeKind.Utc),
            Image = entity.Image,
        };

    public static BonusRareCampaignSnapshot ToSnapshot(this BonusRareCampaignEntity entity) =>
        new()
        {
            Id = entity.Id,
            Code = entity.Code,
            FurnitureName = entity.FurnitureName,
            ProductCode = entity.ProductCode,
            CreditsRequired = entity.CreditsRequired,
            Source = entity.Source,
            StartsAt = DateTime.SpecifyKind(entity.StartsAt, DateTimeKind.Utc),
            EndsAt = entity.EndsAt is { } ends
                ? DateTime.SpecifyKind(ends, DateTimeKind.Utc)
                : null,
        };
}
