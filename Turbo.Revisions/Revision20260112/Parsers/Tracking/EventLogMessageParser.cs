using Turbo.Primitives.Messages.Incoming.Tracking;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Tracking;

internal class EventLogMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new EventLogMessage
        {
            Event = packet.PopString(),
            Data = packet.PopString(),
            Action = packet.PopString(),
            ExtraString = packet.PopString(),
            ExtraInt = packet.PopInt(),
        };
}
