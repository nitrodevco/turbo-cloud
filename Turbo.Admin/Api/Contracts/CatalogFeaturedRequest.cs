namespace Turbo.Admin.Api.Contracts;

/// <summary>Every featured item of the front page, in the order it shows them; it replaces them all.</summary>
public sealed record CatalogFeaturedRequest(CatalogFeaturedItemRequest[]? Items);
