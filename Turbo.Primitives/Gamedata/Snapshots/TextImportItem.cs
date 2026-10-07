using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>One of Habbo's texts an import adds, updates or leaves at the hotel's value.</summary>
[GenerateSerializer, Immutable]
public sealed record TextImportItem
{
    [Id(0)]
    public required string Key { get; init; }

    [Id(1)]
    public required FurnitureImportAction Action { get; init; }

    /// <summary>The hotel's value; null when it has none (a text being added, or one it removed).</summary>
    [Id(2)]
    public string? Current { get; init; }

    [Id(3)]
    public required string Incoming { get; init; }
}
