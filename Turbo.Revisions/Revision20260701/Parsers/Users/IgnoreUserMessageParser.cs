using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Users;

internal class IgnoreUserMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new IgnoreUserMessage { PlayerId = packet.PopInt() };
}
