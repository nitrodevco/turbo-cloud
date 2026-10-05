namespace Turbo.Admin.Api.Contracts;

/// <summary>One page as the editor shows it: everything it sets, and its offers, oldest first.</summary>
public sealed record CatalogPageDetail(
    int Id,
    int? ParentId,
    string CatalogType,
    string Localization,
    string? Name,
    int Icon,
    string Layout,
    string[] ImageData,
    string[] TextData,
    bool Visible,
    CatalogOfferItem[] Offers
);
