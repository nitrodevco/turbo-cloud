using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.FriendList;

public class AcceptFriendMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var friends = packet.PopList(bytesPerItem: 4, p => p.PopInt());

        return new AcceptFriendMessage { Friends = friends };
    }
}
