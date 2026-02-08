using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Navigator;

internal class GuildBaseSearchMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GuildBaseSearchMessage { Unknown = packet.PopInt() };
}
