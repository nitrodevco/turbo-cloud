using System;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Room.Engine.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Furni.Data;

internal class FurnitureItemSerializer
{
    public static void Serialize(IServerPacket packet, FurnitureItemSnapshot item)
    {
        var type = item.Definition.ProductType;

        WriteHead(packet, item);

        packet.WriteInteger(item.SpriteId).WriteInteger((int)item.Definition.FurniCategory);

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

    /// <summary>
    /// Id, type letter and ref: the start of every packet that lists an inventory item. The
    /// client locks an item in the inventory while it sits in a trade (or the recycler, or the
    /// marketplace) by matching this ref, so every packet has to write it the same way — a wall
    /// item's ref is its id, a floor item's is its id negated.
    /// </summary>
    public static void WriteHead(IServerPacket packet, FurnitureItemSnapshot item)
    {
        var type = item.Definition.ProductType;

        packet
            .WriteInteger(item.ItemId)
            .WriteString(type.ToLegacyString().ToUpperInvariant())
            .WriteInteger(
                type == ProductType.Wall ? Math.Abs(item.ItemId) : -Math.Abs(item.ItemId)
            );
    }
}
