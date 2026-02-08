using System.Collections.Generic;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.FriendList;

public class DeclineFriendMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var declineAll = packet.PopBoolean();

        if (declineAll)
        {
            return new DeclineFriendMessage { DeclineAll = declineAll };
        }
        else
        {
            var friendsCount = packet.PopInt();

            var friends = new List<int>(friendsCount);

            for (var i = 0; i < friendsCount; i++)
            {
                var userId = packet.PopInt();

                friends.Add(userId);
            }

            return new DeclineFriendMessage { DeclineAll = declineAll, Friends = friends };
        }
    }
}
