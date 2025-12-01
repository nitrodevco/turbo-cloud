using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots;

public sealed record CompiledRoomModelSnapshot(
    int Width,
    int Height,
    double[] Heights,
    RoomTileFlags[] Flags
);
