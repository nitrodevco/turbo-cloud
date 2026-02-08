using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class RoomEntryInfoMessageComposerSerializer(int header)
    : AbstractSerializer<RoomEntryInfoMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomEntryInfoMessageComposer message)
    {
        packet.WriteInteger(message.RoomId).WriteBoolean(message.IsOwner);
    }
}
