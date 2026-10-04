using System.Collections.Generic;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.LoadBots.Protocol.Decoders;

/// <summary>A furni in the inventory.</summary>
public sealed record InventoryItem(int ItemId, bool IsFloor, int SpriteId, int RoomId);

/// <summary>One fragment of the inventory list.</summary>
public sealed record FurniListFragment(
    int TotalFragments,
    int FragmentIndex,
    IReadOnlyList<InventoryItem> Items
);

/// <summary>
/// Decoders for the furni inventory messages, mirroring
/// <c>Turbo.Revisions/Revision20260909/Serializers/Inventory/Furni</c>.
/// </summary>
public static class InventoryDecoders
{
    public static FurniListFragment FurniList(PacketReader reader)
    {
        var total = reader.Int();
        var index = reader.Int();

        return new FurniListFragment(total, index, Items(reader));
    }

    public static List<InventoryItem> FurniListAddOrUpdate(PacketReader reader) => Items(reader);

    public static int FurniListRemove(PacketReader reader) => reader.Int();

    private static List<InventoryItem> Items(PacketReader reader)
    {
        var count = reader.Count();
        var items = new List<InventoryItem>(count);

        for (var i = 0; i < count; i++)
            items.Add(Item(reader));

        return items;
    }

    private static InventoryItem Item(PacketReader reader)
    {
        var itemId = reader.Int();
        // The type letter, upper-cased (FurnitureItemSerializer.WriteHead).
        var isFloor = reader.String().ToLowerInvariant().FromLegacyString() is ProductType.Floor;
        _ = reader.Int(); // ref
        var spriteId = reader.Int();
        _ = reader.Int(); // category
        _ = RoomDecoders.StuffData(reader);
        _ = reader.Bool(); // recycle
        _ = reader.Bool(); // trade
        _ = reader.Bool(); // group
        _ = reader.Bool(); // sell
        _ = reader.Int(); // seconds to expire
        _ = reader.Bool(); // rent started
        var roomId = reader.Int();

        if (isFloor)
        {
            _ = reader.String(); // slot id
            _ = reader.Int(); // extra
        }

        return new InventoryItem(itemId, isFloor, spriteId, roomId);
    }
}
