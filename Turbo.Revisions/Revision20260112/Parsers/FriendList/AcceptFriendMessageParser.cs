using System.Collections.Generic;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.FriendList;

public class AcceptFriendMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var friendsCount = packet.PopInt();

        var friends = new List<int>(friendsCount);

        for (var i = 0; i < friendsCount; i++)
        {
            var userId = packet.PopInt();

            friends.Add(userId);
        }

        return new AcceptFriendMessage { Friends = friends };
    }
}
