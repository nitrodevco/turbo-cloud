using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>
/// The wrapping the catalog's gift dialog offers. A paid present is one of
/// <see cref="StuffTypes"/> in a box style of <see cref="BoxTypes"/> tied with a ribbon of
/// <see cref="RibbonTypes"/>, and costs <see cref="Price"/> credits on top of the offer; the free
/// one is one of <see cref="DefaultStuffTypes"/> with no style. Furniture is named by sprite id,
/// as the client draws it.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GiftWrappingSnapshot
{
    [Id(0)]
    public required bool Enabled { get; init; }

    [Id(1)]
    public required int Price { get; init; }

    [Id(2)]
    public required ImmutableArray<int> StuffTypes { get; init; }

    [Id(3)]
    public required ImmutableArray<int> BoxTypes { get; init; }

    [Id(4)]
    public required ImmutableArray<int> RibbonTypes { get; init; }

    [Id(5)]
    public required ImmutableArray<int> DefaultStuffTypes { get; init; }
}
