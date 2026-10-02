using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Achievements;

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
        {
            AchievementSnapshotSerializer.Serialize(packet, achievement);
        }

        packet.WriteString(message.DefaultCategory);
    }
}
