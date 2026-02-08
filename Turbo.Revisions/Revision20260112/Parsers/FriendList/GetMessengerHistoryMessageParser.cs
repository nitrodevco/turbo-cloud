using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.FriendList;

internal class GetMessengerHistoryMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetMessengerHistoryMessage { ChatId = packet.PopInt(), Message = packet.PopString() };
}
