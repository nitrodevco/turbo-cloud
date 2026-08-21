using Turbo.Primitives.Messages.Incoming.Campaign;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Campaign;

internal class OpenCampaignCalendarDoorMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new OpenCampaignCalendarDoorMessage();
}
