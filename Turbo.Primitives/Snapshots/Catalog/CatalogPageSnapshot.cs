using System.Collections.Generic;

namespace Turbo.Primitives.Snapshots.Catalog;

public record CatalogPageSnapshot(
    int Id,
    int ParentId,
    string Localization,
    string? Name,
    int Icon,
    string Layout,
    List<string>? ImageData,
    List<string>? TextData,
    bool Visible
);
