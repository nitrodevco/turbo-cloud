using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Notifications;

internal class NotificationDialogMessageComposerSerializer(int header)
    : AbstractSerializer<NotificationDialogMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        NotificationDialogMessageComposer message
    )
    {
        //
    }
}
