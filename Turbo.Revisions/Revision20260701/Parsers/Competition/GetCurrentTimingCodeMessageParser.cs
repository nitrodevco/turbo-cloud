using Turbo.Primitives.Messages.Incoming.Competition;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Competition;

internal class GetCurrentTimingCodeMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetCurrentTimingCodeMessage { SlotConfig = packet.PopString() };
}
