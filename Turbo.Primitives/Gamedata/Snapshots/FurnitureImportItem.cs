using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>One of Habbo's items an import adds, updates or leaves at the hotel's values.</summary>
[GenerateSerializer, Immutable]
public sealed record FurnitureImportItem
{
    [Id(0)]
    public required ProductType ProductType { get; init; }

    [Id(1)]
    public required string ClassName { get; init; }

    /// <summary>Habbo's sprite id for it.</summary>
    [Id(2)]
    public required int SpriteId { get; init; }

    /// <summary>The definition it updates; null for an item being added.</summary>
    [Id(3)]
    public int? DefinitionId { get; init; }

    [Id(4)]
    public required FurnitureImportAction Action { get; init; }

    /// <summary>The fields that differ; empty for an item being added.</summary>
    [Id(5)]
    public required ImmutableArray<FurnitureFieldChange> Fields { get; init; }

    /// <summary>
    /// The logic an item being added gets: the one registered under its classname, otherwise the
    /// default. Null for an item being updated, whose logic is the hotel's.
    /// </summary>
    [Id(6)]
    public string? Logic { get; init; }
}
