using System.Collections.Immutable;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A page of the hand-item catalogue, found by id, name or figure library.</summary>
public sealed record HandItemCatalogPage
{
    public required ImmutableArray<HandItemSnapshot> Items { get; init; }

    /// <summary>Hand items found, on every page.</summary>
    public required int Total { get; init; }

    public required int PageSize { get; init; }

    /// <summary>The revision of the whole catalogue, not of this page.</summary>
    public string? Revision { get; init; }
    public string? Message { get; init; }
}
