using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Notifications;

internal class PetLevelNotificationEventMessageComposerSerializer(int header)
    : AbstractSerializer<PetLevelNotificationEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PetLevelNotificationEventMessageComposer message
    )
    {
        //
    }
}
