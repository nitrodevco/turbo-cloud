using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.FriendList;

public class VisitUserMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new VisitUserMessage { PlayerName = packet.PopString() };
}
