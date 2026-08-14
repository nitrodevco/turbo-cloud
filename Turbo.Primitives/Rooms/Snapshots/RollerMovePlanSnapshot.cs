using System.Collections.Generic;

namespace Turbo.Primitives.Rooms.Snapshots;

public sealed record RollerMovePlanSnapshot
{
    public required int RollerId { get; init; }
    public required int FromIdx { get; init; }
    public required int ToIdx { get; init; }
    public required List<RollerMovedObjectSnapshot> MovedFloorItems { get; init; }
    public required List<RollerMovedObjectSnapshot> MovedAvatars { get; init; }
}
