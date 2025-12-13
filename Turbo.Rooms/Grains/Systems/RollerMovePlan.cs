using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains.Systems;

internal sealed record RollerMovePlan
{
    public required IRoomObject RoomObject { get; init; }
    public required int FromIdx { get; init; }
    public required int ToIdx { get; init; }
    public required double FromZ { get; init; }
    public required double ToZ { get; init; }
}
