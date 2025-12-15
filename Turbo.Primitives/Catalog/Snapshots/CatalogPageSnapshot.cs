using System.Collections.Generic;
using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

[GenerateSerializer, Immutable]
public sealed record CatalogPageSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required int ParentId { get; init; }

    [Id(2)]
    public required string Localization { get; init; }

    [Id(3)]
    public string? Name { get; init; }

    [Id(4)]
    public required int Icon { get; init; }

    [Id(5)]
    public required string Layout { get; init; }

    [Id(6)]
    public required List<string> ImageData { get; init; }

    [Id(7)]
    public required List<string> TextData { get; init; }

    [Id(8)]
    public required bool Visible { get; init; }

    [Id(9)]
    public required ImmutableArray<int> OfferIds { get; init; }

    [Id(10)]
    public required ImmutableArray<int> ChildIds { get; init; }
}
