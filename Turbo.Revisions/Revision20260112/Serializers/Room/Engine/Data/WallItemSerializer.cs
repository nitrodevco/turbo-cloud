using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine.Data;

internal class WallItemSerializer
{
    public static void Serialize(IServerPacket packet, RoomWallItemSnapshot item)
    {
        packet
            .WriteString(item.ObjectId.ToString())
            .WriteInteger(item.SpriteId)
            .WriteString(item.WallPosition);

        if (item.StuffData is LegacyStuffSnapshot legacy)
            packet.WriteString(legacy.Data);

        packet
            .WriteInteger(-1) // expiration
            .WriteInteger((int)item.UsagePolicy)
            .WriteInteger(item.OwnerId);
    }
}
