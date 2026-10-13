using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class ScrSendKickbackInfoMessageComposerSerializer(int header)
    : AbstractSerializer<ScrSendKickbackInfoMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ScrSendKickbackInfoMessageComposer message
    )
    {
        packet
            .WriteInteger(message.CurrentHcStreak)
            .WriteString(message.FirstSubscriptionDate)
            .WriteDouble(message.KickbackPercentage)
            .WriteInteger(message.TotalCreditsMissed)
            .WriteInteger(message.TotalCreditsRewarded)
            .WriteInteger(message.TotalCreditsSpent)
            .WriteInteger(message.CreditRewardForStreakBonus)
            .WriteInteger(message.CreditRewardForMonthlySpent)
            .WriteInteger(message.TimeUntilPayday);
    }
}
