using Turbo.Primitives.Packets;
using Turbo.Primitives.Players.Snapshots.Achievements;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Achievements.Snapshots;

internal class PlayerAchievementProgressSnapshotSerializer
{
    public static void Serialize(IServerPacket packet, PlayerAchievementProgressSnapshot message)
    {
        packet
            .WriteInteger(message.AchievementId)
            .WriteString(message.Code)
            .WriteString(message.Category)
            .WriteInteger(message.Level)
            .WriteInteger(message.MaxLevel)
            .WriteInteger(message.Progress)
            .WriteInteger(message.LevelGoal)
            .WriteInteger(message.NextLevelGoal)
            .WriteInteger((int)message.State);
    }
}
