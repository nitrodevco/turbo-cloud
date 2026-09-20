using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Notifications;

internal class NotificationDialogMessageComposerSerializer(int header)
    : AbstractSerializer<NotificationDialogMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        NotificationDialogMessageComposer message
    )
    {
        packet.WriteString(message.NotificationType).WriteInteger(message.Parameters.Count);

        foreach (var (key, value) in message.Parameters)
            packet.WriteString(key).WriteString(value);
    }
}
