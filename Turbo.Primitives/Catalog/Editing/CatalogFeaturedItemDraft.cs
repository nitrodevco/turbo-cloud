using System;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Primitives.Catalog.Editing;

/// <summary>
/// A featured item of the catalog's front page as an editor sets it: its title, its promo
/// picture (a path the client puts after <c>image.library.url</c>), what it opens (a page by
/// name, an offer by id, or a product code) and when it comes off, null for never. Its place is
/// where it is in the list saved.
/// </summary>
public sealed record CatalogFeaturedItemDraft(
    string Title,
    string Image,
    CatalogFrontPageItemType Type,
    string Value,
    DateTime? ExpiresAtUtc
);
