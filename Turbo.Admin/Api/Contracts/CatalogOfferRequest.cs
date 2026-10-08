namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// An offer as the editor saves it. <see cref="Products"/>, when set, is everything it gives and
/// replaces what it gave (<see cref="Product"/> is then not read); otherwise a null
/// <see cref="Product"/> leaves what it gives alone. <see cref="ClubGiftDaysRequired"/> makes it
/// a club gift.
/// </summary>
public sealed record CatalogOfferRequest(
    int PageId,
    string? LocalizationId,
    int CostCredits,
    int CostCurrency,
    int? CurrencyTypeId,
    bool CanGift,
    bool CanBundle,
    int ClubLevel,
    bool Visible,
    CatalogProductRequest? Product,
    int? ClubGiftDaysRequired = null,
    CatalogProductRequest[]? Products = null
);
