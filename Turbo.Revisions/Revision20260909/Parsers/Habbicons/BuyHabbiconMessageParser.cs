using Turbo.Primitives.Messages.Incoming.Habbicons;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Habbicons;

internal class BuyHabbiconMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new BuyHabbiconMessage { HabbiconId = packet.PopInt() };
}
