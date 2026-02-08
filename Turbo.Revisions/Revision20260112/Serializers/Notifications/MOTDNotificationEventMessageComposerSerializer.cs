using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Notifications;

internal class MOTDNotificationEventMessageComposerSerializer(int header)
    : AbstractSerializer<MOTDNotificationEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MOTDNotificationEventMessageComposer message
    )
    {
        packet.WriteInteger(message.Messages.Count);

        foreach (var motd in message.Messages)
            packet.WriteString(motd);
    }
}
