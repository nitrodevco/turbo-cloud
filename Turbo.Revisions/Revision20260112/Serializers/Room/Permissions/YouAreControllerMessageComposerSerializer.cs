using Turbo.Primitives.Messages.Outgoing.Room.Permissions;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Permissions;

internal class YouAreControllerMessageComposerSerializer(int header)
    : AbstractSerializer<YouAreControllerMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, YouAreControllerMessageComposer message)
    {
        packet.WriteInteger(message.RoomId).WriteInteger((int)message.ControllerLevel);
    }
}
