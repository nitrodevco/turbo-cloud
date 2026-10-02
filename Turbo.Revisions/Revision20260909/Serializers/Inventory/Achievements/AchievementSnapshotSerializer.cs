using Turbo.Primitives.Achievements.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Achievements;

internal static class AchievementSnapshotSerializer
{
    public static void Serialize(IServerPacket packet, AchievementSnapshot achievement)
    {
        packet
            .WriteInteger(achievement.AchievementId)
            .WriteInteger(achievement.Level)
            .WriteString(achievement.BadgeId)
            .WriteInteger(achievement.ScoreAtStartOfLevel)
            .WriteInteger(achievement.ScoreLimitTotal)
            .WriteInteger(achievement.LevelRewardPoints)
            .WriteInteger(achievement.LevelRewardPointType)
            .WriteInteger(achievement.CurrentPointsTotal)
            .WriteBoolean(achievement.FinalLevel)
            .WriteString(achievement.Category)
            .WriteString(achievement.SubCategory)
            .WriteInteger(achievement.LevelCount)
            .WriteInteger(achievement.DisplayMethod)
            .WriteShort((short)achievement.State);
    }
}
