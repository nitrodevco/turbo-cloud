namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// One item as a wired snapshot box remembered it (altitude in hundredths). The box keeps a map
/// of item id to entry under <see cref="SECTION"/>.
/// </summary>
public sealed record WiredFurniSnapshotEntry(int State, int Rotation, int X, int Y, int Z)
{
    public const string SECTION = "wired_snapshot";
}
