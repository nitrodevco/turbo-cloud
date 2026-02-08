using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Achievements;

internal class AchievementsScoreEventMessageComposerSerializer(int header)
    : AbstractSerializer<AchievementsScoreEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        AchievementsScoreEventMessageComposer message
    )
    {
        packet.WriteInteger(message.Score);
    }
}
