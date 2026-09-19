using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class PetSupplementedNotificationEventMessageComposerSerializer(int header)
    : AbstractSerializer<PetSupplementedNotificationEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PetSupplementedNotificationEventMessageComposer message
    )
    {
        packet
            .WriteInteger(message.PetId)
            .WriteInteger(message.PlayerId)
            .WriteInteger((int)message.Supplement);
    }
}
