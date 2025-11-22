namespace Turbo.Primitives.Rooms.Snapshots;

public sealed record CompiledRoomModelSnapshot(
    int Width,
    int Height,
    double[] Heights,
    byte[] States
);
