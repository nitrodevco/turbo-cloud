using System.Collections.Immutable;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A catalogue derived from the hotel's current local client data.</summary>
public sealed record HandItemCatalogSnapshot
{
    public required ImmutableArray<HandItemSnapshot> Items { get; init; }
    public string? Revision { get; init; }
    public string? Message { get; init; }
}
