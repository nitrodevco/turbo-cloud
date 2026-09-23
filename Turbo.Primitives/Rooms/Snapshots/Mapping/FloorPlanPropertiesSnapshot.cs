using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Mapping;

/// <summary>
/// What a floor plan save says about the room around the tiles: where the door is and how the
/// walls and floor are drawn. Every number is still the client's own, so the room checks each
/// one names something before it is kept.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record FloorPlanPropertiesSnapshot
{
    [Id(0)]
    public required int DoorX { get; init; }

    [Id(1)]
    public required int DoorY { get; init; }

    [Id(2)]
    public required int DoorRotation { get; init; }

    [Id(3)]
    public required int WallThickness { get; init; }

    [Id(4)]
    public required int FloorThickness { get; init; }

    /// <summary>The height every wall is drawn at, or -1 to let the client work it out.</summary>
    [Id(5)]
    public required int FixedWallsHeight { get; init; }
}
