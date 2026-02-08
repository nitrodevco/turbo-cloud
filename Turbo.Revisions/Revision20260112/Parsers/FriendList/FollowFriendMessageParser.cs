using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.FriendList;

public class FollowFriendMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new FollowFriendMessage { PlayerId = packet.PopInt() };
}
