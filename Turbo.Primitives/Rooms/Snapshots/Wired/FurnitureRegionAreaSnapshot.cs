using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

/// <summary>One rectangle of a furniture region (Flash reads four ints into a <c>Rectangle</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record FurnitureRegionAreaSnapshot
{
    [Id(0)]
    public required int X { get; init; }

    [Id(1)]
    public required int Y { get; init; }

    [Id(2)]
    public required int Width { get; init; }

    [Id(3)]
    public required int Height { get; init; }
}
