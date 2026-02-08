using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class HeightMapUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<HeightMapUpdateMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, HeightMapUpdateMessageComposer message)
    {
        packet.WriteByte((byte)message.TileHeights.Length);

        foreach (var tile in message.TileHeights)
            packet.WriteByte((byte)tile.X).WriteByte((byte)tile.Y).WriteShort(tile.Height);
    }
}
