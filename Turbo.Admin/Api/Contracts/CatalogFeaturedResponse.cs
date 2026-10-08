namespace Turbo.Admin.Api.Contracts;

/// <summary>The front page's featured items, in their order, published or not.</summary>
public sealed record CatalogFeaturedResponse(CatalogFeaturedItem[] Items);
