using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Action;

internal class SleepMessageComposerSerializer(int header)
    : AbstractSerializer<SleepMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, SleepMessageComposer message)
    {
        packet.WriteInteger(message.UserId).WriteBoolean(message.IsSleeping);
    }
}
