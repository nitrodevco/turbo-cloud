namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// Where a room linker furni leads, under <see cref="SECTION"/> in the item or definition
/// extra data. The "teleport to room" wired action reads it from the linker it picked.
/// </summary>
public sealed record RoomLinkerData
{
    public const string SECTION = "room_linker";

    public int RoomId { get; init; }
}
