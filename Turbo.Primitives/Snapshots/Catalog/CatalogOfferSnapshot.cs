namespace Turbo.Primitives.Snapshots.Catalog;

public record CatalogOfferSnapshot(
    int Id,
    int PageId,
    string LocalizationId,
    bool Rentable,
    int CostCredits,
    int CostCurrency,
    int? CurrencyType,
    int CostSilver,
    bool CanGift,
    bool CanBundle,
    int ClubLevel,
    bool Visible
);
