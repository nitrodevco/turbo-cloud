using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A catalog page that shows a picture.</summary>
public sealed record CatalogImagePage(int Id, string Title);

/// <summary>
/// A catalog picture: a file in <c>images/catalog</c>, <see cref="Name"/> as a page gives it (its
/// <c>image_data</c>, the file without <c>.gif</c>), its size and when it was written, and the
/// pages that show it.
/// </summary>
public sealed record CatalogImageItem(
    string Name,
    string File,
    long Size,
    DateTime? UpdatedAtUtc,
    CatalogImagePage[] Pages
);

/// <summary>A page of the catalog pictures, with how many match in all.</summary>
public sealed record CatalogImagesResponse(int Total, CatalogImageItem[] Items);

/// <summary>Habbo's catalog pictures to take in, by the names the pages give them.</summary>
public sealed record CatalogPicturesRequest(string[]? Names);
