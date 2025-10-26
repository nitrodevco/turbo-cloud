namespace Turbo.Primitives.Snapshots.Catalog;

public record CatalogOfferSnapshot(
    int Id,
    int PageId,
    string LocalizationId,
    int CostCredits,
    int CostCurrency,
    int? CurrencyType,
    bool CanGift,
    bool CanBundle,
    int ClubLevel,
    bool Visible
);
