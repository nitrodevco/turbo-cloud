using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;

namespace Turbo.Revisions.Revision20260909.Parsers.FriendList;

public class RemoveFriendMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var friendIds = packet.PopList(bytesPerItem: 4, p => PlayerId.Parse(p.PopInt()));

        return new RemoveFriendMessage { FriendIds = friendIds };
    }
}
