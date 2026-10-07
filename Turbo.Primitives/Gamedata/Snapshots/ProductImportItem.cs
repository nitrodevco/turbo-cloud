using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>One of Habbo's products an import adds, updates or leaves at the hotel's values.</summary>
[GenerateSerializer, Immutable]
public sealed record ProductImportItem
{
    [Id(0)]
    public required string Code { get; init; }

    [Id(1)]
    public required FurnitureImportAction Action { get; init; }

    /// <summary>The fields that differ (<c>name</c>, <c>description</c>); for a product being added, both.</summary>
    [Id(2)]
    public required ImmutableArray<FurnitureFieldChange> Fields { get; init; }
}
