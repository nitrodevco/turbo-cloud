using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class ScrSendUserInfoMessageSerializer(int header)
    : AbstractSerializer<ScrSendUserInfoMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ScrSendUserInfoMessageComposer message)
    {
        packet
            .WriteString(message.ProductName)
            .WriteInteger(message.DaysToPeriodEnd)
            .WriteInteger(message.MemberPeriods)
            .WriteInteger(message.PeriodsSubscribedAhead)
            .WriteInteger(message.ResponseType)
            .WriteBoolean(message.HasEverBeenMember)
            .WriteBoolean(message.IsVIP)
            .WriteInteger(message.PastClubDays)
            .WriteInteger(message.PastVipDays)
            .WriteInteger(message.MinutesUntilExpiration)
            .WriteInteger(message.MinutesSinceLastModified);
    }
}
