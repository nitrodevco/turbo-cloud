using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// One field of a furnidata item that an import touches, by its furnidata key (<c>xdim</c>,
/// <c>name</c>, ...). Values are JSON, as furnidata writes them.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record FurnitureFieldChange
{
    [Id(0)]
    public required string Field { get; init; }

    /// <summary>The hotel's value.</summary>
    [Id(1)]
    public required string Current { get; init; }

    /// <summary>Habbo's new value.</summary>
    [Id(2)]
    public required string Incoming { get; init; }

    /// <summary>The hotel changed this field itself, so it keeps its value over Habbo's.</summary>
    [Id(3)]
    public required bool Kept { get; init; }
}
