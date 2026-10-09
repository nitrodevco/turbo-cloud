using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

internal class CommunityGoalProgressMessageComposerSerializer(int header)
    : AbstractSerializer<CommunityGoalProgressMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CommunityGoalProgressMessageComposer message
    )
    {
        var data = message.Data;

        packet.WriteBoolean(data.HasGoalExpired);
        packet.WriteInteger(data.PersonalContributionScore);
        packet.WriteInteger(data.PersonalContributionRank);
        packet.WriteInteger(data.CommunityTotalScore);
        packet.WriteInteger(data.CommunityHighestAchievedLevel);
        packet.WriteInteger(data.ScoreRemainingUntilNextLevel);
        packet.WriteInteger(data.PercentCompletionTowardsNextLevel);
        packet.WriteString(data.GoalCode);
        packet.WriteInteger(data.TimeRemainingInSeconds);
        packet.WriteInteger(data.RewardUserLimits.Length);

        foreach (var limit in data.RewardUserLimits)
            packet.WriteInteger(limit);
    }
}
