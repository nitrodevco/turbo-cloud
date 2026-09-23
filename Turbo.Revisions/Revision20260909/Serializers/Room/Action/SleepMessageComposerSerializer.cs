using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Action;

internal class SleepMessageComposerSerializer(int header)
    : AbstractSerializer<SleepMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, SleepMessageComposer message)
    {
        packet.WriteInteger(message.ObjectId).WriteBoolean(message.IsSleeping);
    }
}
