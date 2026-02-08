using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Users;

internal class UnblockUserMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new UnblockUserMessage { PlayerId = packet.PopInt() };
}
