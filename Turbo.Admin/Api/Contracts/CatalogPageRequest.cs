namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A page as the editor saves it. <see cref="ParentId"/> is the page to create it under, and is
/// not read when an existing page is saved: moving is its own request.
/// </summary>
public sealed record CatalogPageRequest(
    int? ParentId,
    string? Localization,
    string? Name,
    int Icon,
    string? Layout,
    string[]? ImageData,
    string[]? TextData,
    bool Visible
);
