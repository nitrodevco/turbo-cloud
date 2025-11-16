namespace Turbo.Primitives.Snapshots.Rooms.Mapping;

public sealed record CompiledRoomModelSnapshot(
    int Width,
    int Height,
    double[] Heights,
    byte[] States
);
