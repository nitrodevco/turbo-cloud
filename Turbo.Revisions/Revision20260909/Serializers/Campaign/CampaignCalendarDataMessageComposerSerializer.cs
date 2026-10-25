using Turbo.Primitives.Messages.Outgoing.Campaign;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Campaign;

/// <summary>Name, image, current day, day count, then the opened and the missed days, each a count and ints (AS3 CampaignCalendarData.parse).</summary>
internal class CampaignCalendarDataMessageComposerSerializer(int header)
    : AbstractSerializer<CampaignCalendarDataMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CampaignCalendarDataMessageComposer message
    )
    {
        packet.WriteString(message.CampaignName);
        packet.WriteString(message.CampaignImage);
        packet.WriteInteger(message.CurrentDay);
        packet.WriteInteger(message.CampaignDays);
        packet.WriteInteger(message.OpenedDays.Length);

        foreach (var day in message.OpenedDays)
            packet.WriteInteger(day);

        packet.WriteInteger(message.MissedDays.Length);

        foreach (var day in message.MissedDays)
            packet.WriteInteger(day);
    }
}
