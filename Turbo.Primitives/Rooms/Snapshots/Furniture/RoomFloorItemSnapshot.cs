using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Furniture;

[GenerateSerializer, Immutable]
public sealed record RoomFloorItemSnapshot : RoomItemSnapshot
{
    /// <summary>What the client reads as the furni's "extras": a song disk's song id, else zero.</summary>
    [Id(0)]
    public int Extra { get; init; }
}
