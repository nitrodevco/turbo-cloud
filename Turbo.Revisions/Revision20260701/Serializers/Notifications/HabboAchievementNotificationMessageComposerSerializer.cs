using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Notifications;

internal class HabboAchievementNotificationMessageComposerSerializer(int header)
    : AbstractSerializer<HabboAchievementNotificationMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        HabboAchievementNotificationMessageComposer message
    )
    {
        //
    }
}
