using System;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Catalog.Editing;

/// <summary>
/// A backup of the catalog, as the editor lists it: its name, who took it and when, whether the
/// editor took it itself before a rollback, and how many of each row it holds.
/// </summary>
public sealed record CatalogBackupSummary(
    int Id,
    string Name,
    PlayerId TakenBy,
    DateTime TakenAtUtc,
    bool Automatic,
    int Pages,
    int Offers,
    int Products,
    int FeaturedItems
);
