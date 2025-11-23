using Turbo.Primitives.Rooms.Mapping;

namespace Turbo.Primitives.Rooms.Snapshots;

public sealed record CompiledRoomModelSnapshot(
    int Width,
    int Height,
    double[] Heights,
    RoomTileFlags[] States
);
