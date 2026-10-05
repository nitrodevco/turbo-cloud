namespace Turbo.Admin.Api.Contracts;

/// <summary>What a publish put live, and how many players online were told to refresh.</summary>
public sealed record CatalogPublishResponse(int Pages, int Offers, int PlayersTold);
