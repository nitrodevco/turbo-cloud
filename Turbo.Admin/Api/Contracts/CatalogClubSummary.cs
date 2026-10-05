namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The normal catalog's club shop: how many memberships and club gifts are on offer, and the
/// shown pages whose layouts (<c>club_buy</c>, <c>club_gifts</c>) let players buy and claim
/// them; null when there is none.
/// </summary>
public sealed record CatalogClubSummary(
    int Memberships,
    int Gifts,
    int? ClubBuyPageId,
    int? ClubGiftsPageId
);
