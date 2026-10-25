using Turbo.Primitives.Messages.Incoming.Campaign;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Campaign;

/// <summary>The campaign's name, then the door's day index (AS3 OpenCampaignCalendarDoorAsStaffComposer).</summary>
internal class OpenCampaignCalendarDoorAsStaffMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new OpenCampaignCalendarDoorAsStaffMessage
        {
            CampaignName = packet.PopString(),
            DayIndex = packet.PopInt(),
        };
}
