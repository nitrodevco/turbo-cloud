using Turbo.Primitives.Messages.Outgoing.Habbicons;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Habbicons;

internal class UserHabbiconStatusChangedMessageComposerSerializer(int header)
    : AbstractSerializer<UserHabbiconStatusChangedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        UserHabbiconStatusChangedMessageComposer message
    ) => packet.WriteInteger(message.HabbiconId).WriteInteger((int)message.State);
}
