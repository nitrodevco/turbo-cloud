using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Notifications;

internal class HabboActivityPointNotificationMessageComposerSerializer(int header)
    : AbstractSerializer<HabboActivityPointNotificationMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        HabboActivityPointNotificationMessageComposer message
    )
    {
        //
    }
}
