using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Layout;

internal class RoomEntryTileMessageComposerSerializer(int header)
    : AbstractSerializer<RoomEntryTileMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomEntryTileMessageComposer message)
    {
        packet.WriteInteger(message.X).WriteInteger(message.Y).WriteInteger((int)message.Rotation);
    }
}
