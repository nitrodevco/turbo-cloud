using Turbo.Primitives.Messages.Incoming.Campaign;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Campaign;

internal class OpenCampaignCalendarDoorAsStaffMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new OpenCampaignCalendarDoorAsStaffMessage();
}
