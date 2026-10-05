namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// One offer on a page: its name key, its price (credits, and an amount of a currency), who may
/// buy it, whether it shows, whether it is a club gift (and the club days it needs), and what it
/// gives.
/// </summary>
public sealed record CatalogOfferItem(
    int Id,
    string LocalizationId,
    int CostCredits,
    int CostCurrency,
    int? CurrencyTypeId,
    bool CanGift,
    bool CanBundle,
    int ClubLevel,
    bool Visible,
    bool IsClubGift,
    int? ClubGiftDaysRequired,
    CatalogProductItem[] Products
);
