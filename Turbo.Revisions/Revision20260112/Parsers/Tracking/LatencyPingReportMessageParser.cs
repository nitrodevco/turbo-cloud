using Turbo.Primitives.Messages.Incoming.Tracking;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Tracking;

internal class LatencyPingReportMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new LatencyPingReportMessage();
}
