using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A furniture definition as the list shows it: what it is, and its tags.</summary>
[GenerateSerializer, Immutable]
public sealed record FurnitureListItem
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required ProductType ProductType { get; init; }

    [Id(2)]
    public required string ClassName { get; init; }

    /// <summary>The name players see (furnidata's <c>name</c>).</summary>
    [Id(3)]
    public string? Name { get; init; }

    [Id(4)]
    public required int SpriteId { get; init; }

    [Id(5)]
    public string? FurniLine { get; init; }

    /// <summary>Furnidata's <c>category</c>.</summary>
    [Id(6)]
    public string? Category { get; init; }

    /// <summary>The bundle it is drawn from: its classname without the <c>*N</c> colour.</summary>
    [Id(7)]
    public required string AssetName { get; init; }

    [Id(8)]
    public required ImmutableArray<FurnitureTag> Tags { get; init; }
}
