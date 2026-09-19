namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>One item as a wired snapshot box remembered it (altitude in hundredths).</summary>
public sealed record WiredFurniSnapshotEntry(int State, int Rotation, int X, int Y, int Z);
