using System.Collections.Generic;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Players.Enums.Wallet;

namespace Turbo.Admin.Catalog.Contracts;

internal sealed record CatalogPageTreeItem(
    int Id,
    int? ParentId,
    string Localization,
    string? Name,
    int Icon,
    string Layout,
    int SortOrder,
    bool Visible,
    int OfferCount,
    List<CatalogPageTreeItem> Children
);

internal sealed record CatalogOfferSummary(
    int Id,
    string LocalizationId,
    int CostCredits,
    int CostCurrency,
    int? CurrencyTypeId,
    int ClubLevel,
    bool Visible,
    int ProductCount
);

internal sealed record CatalogPageDetailResponse(
    int Id,
    int? ParentId,
    string Localization,
    string? Name,
    int Icon,
    string Layout,
    List<string> ImageData,
    List<string> TextData,
    bool Visible,
    CatalogOfferSummary[] Offers
);

internal sealed record CatalogProductItem(
    int Id,
    ProductType ProductType,
    int? FurnitureDefinitionId,
    string? FurnitureName,
    int? FurnitureSpriteId,
    string? ExtraParam,
    int Quantity
);

internal sealed record CatalogOfferDetailResponse(
    int Id,
    int PageId,
    string LocalizationId,
    int CostCredits,
    int CostCurrency,
    int? CurrencyTypeId,
    bool CanGift,
    bool CanBundle,
    int ClubLevel,
    bool Visible,
    CatalogProductItem[] Products
);

internal sealed record UpsertPageRequest(
    int? ParentId,
    string Localization,
    string? Name,
    int Icon,
    string Layout,
    List<string>? ImageData,
    List<string>? TextData,
    bool Visible
);

internal sealed record PageOrderEntryRequest(int PageId, int? ParentId, int SortOrder);

internal sealed record ReorderPagesRequest(PageOrderEntryRequest[] Entries);

internal sealed record UpsertOfferRequest(
    int PageId,
    string LocalizationId,
    int CostCredits,
    int CostCurrency,
    int? CurrencyTypeId,
    bool CanGift,
    bool CanBundle,
    int ClubLevel,
    bool Visible
);

internal sealed record UpsertProductRequest(
    int OfferId,
    ProductType ProductType,
    int? FurnitureDefinitionId,
    string? ExtraParam,
    int Quantity
);

internal sealed record FurnitureDefinitionListItem(
    int Id,
    string Name,
    int SpriteId,
    ProductType ProductType
);

internal sealed record CurrencyTypeListItem(int Id, string Name, CurrencyType CurrencyType);

internal sealed record CreatedResponse(int Id);
