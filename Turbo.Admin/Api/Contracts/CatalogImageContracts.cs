using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A catalog page that shows a picture.</summary>
public sealed record CatalogImagePage(int Id, string Title);

/// <summary>
/// A catalog picture: <see cref="Name"/> as a page gives it (its <c>image_data</c>), the file the
/// client loads it from (<c>images/catalog/&lt;name&gt;.gif</c>), where the file came from
/// (<c>habbo</c>, <c>upload</c>) and the pages that show it. A picture a page names that this
/// server has no file for has <see cref="HasFile"/> false, and no source when nothing ever
/// fetched it.
/// </summary>
public sealed record CatalogImageItem(
    string Name,
    string File,
    string? Source,
    bool HasFile,
    long Size,
    DateTime? UpdatedAtUtc,
    CatalogImagePage[] Pages
);

/// <summary>A page of the catalog pictures, with how many match in all.</summary>
public sealed record CatalogImagesResponse(int Total, CatalogImageItem[] Items);
