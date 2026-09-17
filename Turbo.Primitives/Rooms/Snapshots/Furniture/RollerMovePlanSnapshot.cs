using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Furniture;

[GenerateSerializer, Immutable]
public sealed record RollerMovePlanSnapshot
{
    [Id(0)]
    public required int RollerId { get; init; }

    [Id(1)]
    public required int FromIdx { get; init; }

    [Id(2)]
    public required int ToIdx { get; init; }

    [Id(3)]
    public required List<RollerMovedObjectSnapshot> MovedFloorItems { get; init; }

    [Id(4)]
    public required List<RollerMovedObjectSnapshot> MovedAvatars { get; init; }
}
