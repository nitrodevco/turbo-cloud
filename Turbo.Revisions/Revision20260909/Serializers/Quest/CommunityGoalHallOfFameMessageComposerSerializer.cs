using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

internal class CommunityGoalHallOfFameMessageComposerSerializer(int header)
    : AbstractSerializer<CommunityGoalHallOfFameMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CommunityGoalHallOfFameMessageComposer message
    )
    {
        packet.WriteString(message.GoalCode);
        packet.WriteInteger(message.Contributors.Length);

        foreach (var contributor in message.Contributors)
        {
            packet.WriteInteger(contributor.PlayerId);
            packet.WriteString(contributor.Name);
            packet.WriteString(contributor.Figure);
            packet.WriteInteger(contributor.Rank);
            packet.WriteInteger(contributor.Score);
        }
    }
}
