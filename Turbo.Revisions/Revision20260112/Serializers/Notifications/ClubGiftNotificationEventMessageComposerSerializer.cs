using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Notifications;

internal class ClubGiftNotificationEventMessageComposerSerializer(int header)
    : AbstractSerializer<ClubGiftNotificationEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ClubGiftNotificationEventMessageComposer message
    )
    {
        //
    }
}
