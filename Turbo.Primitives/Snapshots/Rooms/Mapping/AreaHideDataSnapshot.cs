namespace Turbo.Primitives.Snapshots.Rooms.Mapping;

public sealed record AreaHideDataSnapshot(
    int FurniId,
    bool On,
    int RootX,
    int RootY,
    int Width,
    int Length,
    bool Invert
);
