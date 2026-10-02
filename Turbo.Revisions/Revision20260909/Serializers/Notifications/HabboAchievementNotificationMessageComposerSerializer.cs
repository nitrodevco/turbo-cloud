using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Notifications;

internal class HabboAchievementNotificationMessageComposerSerializer(int header)
    : AbstractSerializer<HabboAchievementNotificationMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        HabboAchievementNotificationMessageComposer message
    )
    {
        packet
            .WriteInteger(message.Type)
            .WriteInteger(message.Level)
            .WriteInteger(message.BadgeId)
            .WriteString(message.BadgeCode)
            .WriteInteger(message.PointsTotal)
            .WriteInteger(message.LevelRewardPoints)
            .WriteInteger(message.LevelRewardPointType)
            .WriteInteger(message.BonusPoints)
            .WriteInteger(message.AchievementId)
            .WriteString(message.RemovedBadgeCode)
            .WriteString(message.Category)
            .WriteBoolean(message.ShowDialogToUser)
            .WriteInteger(message.OwnerCount)
            .WriteInteger(message.BadgeRarityId);
    }
}
