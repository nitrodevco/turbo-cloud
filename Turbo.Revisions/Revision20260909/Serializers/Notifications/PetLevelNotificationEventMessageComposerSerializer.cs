using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Pets.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Notifications;

internal class PetLevelNotificationEventMessageComposerSerializer(int header)
    : AbstractSerializer<PetLevelNotificationEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PetLevelNotificationEventMessageComposer message
    )
    {
        packet.WriteInteger(message.PetId).WriteString(message.Name).WriteInteger(message.Level);

        PetFigureSerializer.Serialize(packet, message.Figure);
    }
}
