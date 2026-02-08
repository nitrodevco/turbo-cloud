using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Navigator;

internal class RateFlatMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new RateFlatMessage { Points = packet.PopInt() };
}
