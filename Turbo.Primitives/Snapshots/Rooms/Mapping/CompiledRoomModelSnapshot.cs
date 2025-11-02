namespace Turbo.Primitives.Snapshots.Rooms.Mapping;

public sealed record CompiledRoomModelSnapshot(
    int Id,
    int Width,
    int Height,
    byte[] Heights,
    byte[] States
);
