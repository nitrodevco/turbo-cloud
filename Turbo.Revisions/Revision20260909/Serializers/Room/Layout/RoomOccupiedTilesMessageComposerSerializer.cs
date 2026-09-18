using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Layout;

internal class RoomOccupiedTilesMessageComposerSerializer(int header)
    : AbstractSerializer<RoomOccupiedTilesMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RoomOccupiedTilesMessageComposer message
    )
    {
        packet.WriteInteger(message.Tiles.Length);

        foreach (var tile in message.Tiles)
            packet.WriteInteger(tile.X).WriteInteger(tile.Y);
    }
}
