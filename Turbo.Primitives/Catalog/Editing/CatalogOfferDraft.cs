namespace Turbo.Primitives.Catalog.Editing;

/// <summary>
/// An offer as an editor sets it: the page it is on, its name key, its price (credits, and an
/// amount of the activity-point currency <see cref="CurrencyTypeId"/> names), whether it needs
/// club, can be gifted or bought in bulk, whether it is shown, and what it gives.
/// <see cref="Product"/> is null on an update to leave what it gives as it is.
/// <see cref="ClubGiftDaysRequired"/> makes it a club gift, which members claim rather than buy
/// once they have used up that many days of Habbo Club; null for an offer that is sold.
/// </summary>
public sealed record CatalogOfferDraft(
    int PageId,
    string LocalizationId,
    int CostCredits,
    int CostCurrency,
    int? CurrencyTypeId,
    bool CanGift,
    bool CanBundle,
    int ClubLevel,
    bool Visible,
    CatalogProductDraft? Product,
    int? ClubGiftDaysRequired = null
);
