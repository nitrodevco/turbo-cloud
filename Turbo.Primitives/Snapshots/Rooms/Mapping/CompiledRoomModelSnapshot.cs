namespace Turbo.Primitives.Snapshots.Rooms.Mapping;

public sealed record CompiledRoomModelSnapshot(
    int Width,
    int Height,
    float[] Heights,
    byte[] States
);
