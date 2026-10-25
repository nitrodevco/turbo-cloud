namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A page as the editor saves it. <see cref="ParentId"/> is the page to create it under, and is
/// not read when an existing page is saved: moving is its own request. <see cref="Display"/> is
/// <c>regular</c>, <c>bc_only</c>, <c>both</c> or <c>invisible</c>. <see cref="FolderOnly"/> makes it
/// a heading with no page of its own; left out, a saved page keeps what it was. <see cref="MinRank"/>
/// (1 is everyone) and <see cref="RequiredNode"/> (a permission node; empty takes it off) keep a
/// page and what is under it from players below that rank or without that node; left out, a saved
/// page keeps them.
/// </summary>
public sealed record CatalogPageRequest(
    int? ParentId,
    string? Localization,
    string? Name,
    int Icon,
    string? Layout,
    string[]? ImageData,
    string[]? TextData,
    string? Display,
    bool? FolderOnly = null,
    int? MinRank = null,
    string? RequiredNode = null
);
