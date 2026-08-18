using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Inventory.Achievements.Snapshots;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Achievements;

internal class AchievementsEventMessageComposerSerializer(int header)
    : AbstractSerializer<AchievementsEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        AchievementsEventMessageComposer message
    )
    {
        packet.WriteInteger(message.Achievements.Count);

        foreach (var achievement in message.Achievements)
            PlayerAchievementProgressSnapshotSerializer.Serialize(packet, achievement);
    }
}
