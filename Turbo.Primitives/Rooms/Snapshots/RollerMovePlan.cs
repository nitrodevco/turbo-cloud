using System.Collections.Generic;

namespace Turbo.Primitives.Rooms.Snapshots;

public sealed record RollerMovePlan
{
    public required int RollerId { get; init; }
    public required int FromIdx { get; init; }
    public required int ToIdx { get; init; }
    public required List<RollerMovedObject> MovedFloorItems { get; init; }
    public required List<RollerMovedObject> MovedAvatars { get; init; }
}
