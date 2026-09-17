using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Engine.Data;

internal class WallItemSerializer
{
    public static void Serialize(IServerPacket packet, RoomWallItemSnapshot item)
    {
        packet
            .WriteString(item.ObjectId.ToString())
            .WriteInteger(item.SpriteId)
            .WriteString(item.WallPosition)
            .WriteString(item.StuffData is LegacyStuffSnapshot legacy ? legacy.Data : string.Empty)
            .WriteInteger(-1) // expiration
            .WriteInteger((int)item.UsagePolicy)
            .WriteInteger(item.OwnerId);
    }
}
