namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The normal catalog's club shop: how many memberships and club gifts are on offer, and the
/// shown pages the client opens by name (<c>hc_membership</c>, <c>club_gifts</c>) to buy and
/// claim them; null when there is none.
/// </summary>
public sealed record CatalogClubSummary(
    int Memberships,
    int Gifts,
    int? ClubBuyPageId,
    int? ClubGiftsPageId
);
