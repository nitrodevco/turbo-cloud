using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Users;

internal class RespectUserMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new RespectUserMessage { PlayerId = packet.PopInt() };
}
