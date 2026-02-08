using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine.Data;

internal class FloorItemSerializer
{
    public static void Serialize(IServerPacket packet, RoomFloorItemSnapshot item)
    {
        packet
            .WriteInteger(item.ObjectId)
            .WriteInteger(item.SpriteId)
            .WriteInteger(item.X)
            .WriteInteger(item.Y)
            .WriteInteger((int)item.Rotation)
            .WriteString(item.Z.ToString())
            .WriteString(item.StackHeight.ToString())
            .WriteInteger(0); // extra

        StuffDataSnapshotSerializer.Serialize(packet, item.StuffData);

        packet
            .WriteInteger(-1) // expiration
            .WriteInteger((int)item.UsagePolicy)
            .WriteInteger(item.OwnerId);

        // if no sprite id, WriteString(staticClass)
    }
}
