using Turbo.Primitives.Messages.Incoming.Habbicons;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Habbicons;

internal class FavoriteHabbiconMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new FavoriteHabbiconMessage { HabbiconId = packet.PopInt() };
}
