using System.Collections.Generic;

namespace Turbo.Rooms.Grains.Systems;

internal sealed record RollerMovePlan
{
    public required int RollerId { get; init; }
    public required int FromIdx { get; init; }
    public required int ToIdx { get; init; }
    public required List<RollerMovedEntity> MovedFloorItems { get; init; }
    public required List<RollerMovedEntity> MovedAvatars { get; init; }
}
