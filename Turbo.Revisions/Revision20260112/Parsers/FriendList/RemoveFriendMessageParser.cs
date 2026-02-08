using System.Collections.Generic;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.FriendList;

public class RemoveFriendMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var friendIds = new List<int>();

        var totalFriends = packet.PopInt();

        for (var i = 0; i < totalFriends; i++)
        {
            friendIds.Add(packet.PopInt());
        }

        return new RemoveFriendMessage { FriendIds = friendIds };
    }
}
