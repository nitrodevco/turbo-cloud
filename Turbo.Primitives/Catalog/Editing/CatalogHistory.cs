using System;
using System.Collections.Generic;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Catalog.Editing;

/// <summary>
/// One step of the catalog's history since it was last published: what was done, in words for
/// the editor, by whom and when, and how many edits it was (a page built is one step of many).
/// </summary>
public sealed record CatalogHistoryEntry(string Label, PlayerId Editor, DateTime AtUtc, int Edits);

/// <summary>
/// The catalog's history since it was last published: the steps that can be undone, the newest
/// first; the steps undone that can be done again, the next first; and whether older steps were
/// let go, so that discarding can no longer reach the published catalog.
/// </summary>
public sealed record CatalogHistory(
    IReadOnlyList<CatalogHistoryEntry> Undo,
    IReadOnlyList<CatalogHistoryEntry> Redo,
    bool Truncated
);
