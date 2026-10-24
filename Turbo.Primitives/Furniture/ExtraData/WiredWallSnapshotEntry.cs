namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// One wall item as Match Snapshot remembered it: its state and where it was on the wall (its
/// wall coordinates, offset along the wall, height offset and wall side). The box keeps a map of
/// item id to entry under <see cref="SECTION"/>, beside its floor furni.
/// </summary>
public sealed record WiredWallSnapshotEntry(
    int State,
    int X,
    int Y,
    int WallOffset,
    int Height,
    int Rotation
)
{
    public const string SECTION = "wired_wall_snapshot";
}
