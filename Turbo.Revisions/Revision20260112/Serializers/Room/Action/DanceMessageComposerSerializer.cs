using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Action;

internal class DanceMessageComposerSerializer(int header)
    : AbstractSerializer<DanceMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, DanceMessageComposer message)
    {
        packet.WriteInteger(message.ObjectId).WriteInteger((int)message.DanceType);
    }
}
