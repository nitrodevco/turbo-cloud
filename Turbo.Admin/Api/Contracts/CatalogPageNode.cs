namespace Turbo.Admin.Api.Contracts;

/// <summary>One page of the catalog tree, flat: the panel puts the tree together by parent.</summary>
public sealed record CatalogPageNode(
    int Id,
    int? ParentId,
    string Localization,
    string? Name,
    int Icon,
    string Display,
    int SortOrder,
    int OfferCount
);
