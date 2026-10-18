using Orleans;
using Turbo.Primitives.Movements;

namespace Turbo.Primitives.Rooms.Snapshots.Furniture;

/// <summary>A furni a use takes out of the room for good, as it stood, and why.</summary>
[GenerateSerializer, Immutable]
public sealed record UsedUpItem(
    [property: Id(0)] RoomItemSnapshot Item,
    [property: Id(1)] MovementCause Cause
);
