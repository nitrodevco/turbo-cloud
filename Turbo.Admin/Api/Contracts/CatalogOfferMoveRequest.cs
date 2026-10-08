namespace Turbo.Admin.Api.Contracts;

/// <summary>Where an offer goes: onto which page, and at which place among its offers.</summary>
public sealed record CatalogOfferMoveRequest(int PageId, int Index);
