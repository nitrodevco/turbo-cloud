using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Inventory.Achievements.Snapshots;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Achievements;

internal class AchievementEventMessageComposerSerializer(int header)
    : AbstractSerializer<AchievementEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, AchievementEventMessageComposer message)
    {
        PlayerAchievementProgressSnapshotSerializer.Serialize(packet, message.Achievement);
    }
}
