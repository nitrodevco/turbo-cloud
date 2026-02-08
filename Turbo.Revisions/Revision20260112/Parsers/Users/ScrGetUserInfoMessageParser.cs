using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Users;

public class ScrGetUserInfoMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new ScrGetUserInfoMessage { ProductName = packet.PopString() };
}
