namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// One item as a wired snapshot box remembered it (altitude in hundredths). The box keeps a map
/// of item id to entry under <see cref="SECTION"/>. <paramref name="DefinitionId"/> is what the
/// "place furni" box makes its copies from, since the item itself may be long gone; it is zero
/// in snapshots taken before it was recorded.
/// </summary>
public sealed record WiredFurniSnapshotEntry(
    int State,
    int Rotation,
    int X,
    int Y,
    int Z,
    int DefinitionId = 0
)
{
    public const string SECTION = "wired_snapshot";
}
