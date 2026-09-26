using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Inventory.Furni.Data;
using Turbo.Revisions.Revision20260909.Serializers.Room.Engine.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Trading.Data;

/// <summary>
/// An item in a trade offer as the client's trading parser reads it: like an inventory item,
/// but with the creation date and without the recycle/sell flags.
/// </summary>
internal static class TradeItemSerializer
{
    public static void Serialize(IServerPacket packet, FurnitureItemSnapshot item)
    {
        var type = item.Definition.ProductType;
        var created = item.CreatedAtUtc;

        FurnitureItemSerializer.WriteHead(packet, item);

        packet
            .WriteInteger(item.SpriteId)
            .WriteInteger((int)item.Definition.FurniCategory)
            .WriteBoolean(item.Definition.CanGroup);

        StuffDataSnapshotSerializer.Serialize(packet, item.StuffData);

        packet
            .WriteInteger(created?.Day ?? 0)
            .WriteInteger(created?.Month ?? 0)
            .WriteInteger(created?.Year ?? 0);

        if (type == ProductType.Floor)
            packet.WriteInteger(item.Extra);
    }
}
