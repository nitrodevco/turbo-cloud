using Turbo.Primitives.Messages.Incoming.Habbicons;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Habbicons;

internal class UnfavoriteHabbiconMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new UnfavoriteHabbiconMessage { HabbiconId = packet.PopInt() };
}
