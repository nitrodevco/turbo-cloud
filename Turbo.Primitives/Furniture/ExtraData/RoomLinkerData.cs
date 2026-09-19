namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// Where a furni leads, under <see cref="SECTION"/> in the item or definition extra data.
/// A room linker names a fixed <see cref="RoomId"/>. A teleporter names its paired
/// <see cref="ItemId"/> instead, because the pair can be picked up and placed elsewhere: the
/// room it leads to is wherever that item stands now, so it is looked up, never stored.
/// </summary>
public sealed record RoomLinkerData
{
    public const string SECTION = "room_linker";

    public int RoomId { get; init; }

    public int ItemId { get; init; }
}
