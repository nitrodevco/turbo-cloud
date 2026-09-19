using System.Collections.Immutable;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog.Snapshots;
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
            Visible = entity.Visible,
            OfferIds = offerIds,
            ChildIds = childIds,
        };

    public static CatalogOfferSnapshot ToSnapshot(
        this CatalogOfferEntity entity,
        ImmutableArray<int> productIds,
        ImmutableArray<CatalogProductSnapshot> products
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
            CurrencyTypeId = entity.CurrencyTypeId,
            CanGift = entity.CanGift,
            CanBundle = entity.CanBundle,
            ClubLevel = entity.ClubLevel,
            Visible = entity.Visible,
            ProductIds = productIds,
            Products = products,
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
            SpriteId = definition?.SpriteId ?? -1,
            ExtraParam = entity.ExtraParam,
            Quantity = entity.Quantity,
            UniqueSize = series?.TotalQuantity ?? 0,
            UniqueRemaining = series?.RemainingQuantity ?? 0,
            LtdSeriesId = series?.Id,
            ClassName = definition?.Name,
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
}
