using System.Collections.Generic;

namespace Turbo.Primitives.Catalog.Editing;

/// <summary>
/// What a catalog page shows, as an editor sets it: its title, its internal name, its icon, its
/// layout and the layout's images and texts in order, and whether it is shown. Where it sits in
/// the tree is set apart, by moving it.
/// </summary>
public sealed record CatalogPageDraft(
    string Localization,
    string? Name,
    int Icon,
    string Layout,
    IReadOnlyList<string> ImageData,
    IReadOnlyList<string> TextData,
    bool Visible
);
