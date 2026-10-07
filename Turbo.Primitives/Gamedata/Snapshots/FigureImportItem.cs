using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>One of Habbo's figure records an import adds, updates or leaves at the hotel's values.</summary>
[GenerateSerializer, Immutable]
public sealed record FigureImportItem
{
    [Id(0)]
    public required FigureRecordKind Kind { get; init; }

    [Id(1)]
    public required string Key { get; init; }

    [Id(2)]
    public required FurnitureImportAction Action { get; init; }

    /// <summary>The fields that differ; for a record being added, every one.</summary>
    [Id(3)]
    public required ImmutableArray<FurnitureFieldChange> Fields { get; init; }
}
