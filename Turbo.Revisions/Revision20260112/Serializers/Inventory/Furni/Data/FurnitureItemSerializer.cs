using System;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Room.Engine.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Furni.Data;

internal class FurnitureItemSerializer
{
    public static void Serialize(IServerPacket packet, FurnitureItemSnapshot item)
    {
        var type = item.Definition.ProductType;

        packet
            .WriteInteger(item.ItemId)
            .WriteString(type.ToLegacyString().ToUpper())
            .WriteInteger(type == ProductType.Wall ? Math.Abs(item.ItemId) : -Math.Abs(item.ItemId))
            .WriteInteger(item.SpriteId)
            .WriteInteger((int)item.Definition.FurniCategory);

        StuffDataSnapshotSerializer.Serialize(packet, item.StuffData);

        packet
            .WriteBoolean(item.Definition.CanRecycle)
            .WriteBoolean(item.Definition.CanTrade)
            .WriteBoolean(item.Definition.CanGroup)
            .WriteBoolean(item.Definition.CanSell)
            .WriteInteger(item.SecondsToExpiration)
            .WriteBoolean(item.HasRentPeriodStarted)
            .WriteInteger(item.RoomId);

        if (type == ProductType.Floor)
            packet.WriteString(item.SlotId).WriteInteger(item.Extra);
    }
}
