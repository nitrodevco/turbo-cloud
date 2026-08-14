using System.Collections.Generic;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;

namespace Turbo.Revisions.Revision20260112.Parsers.FriendList;

public class DeclineFriendMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var declineAll = packet.PopBoolean();

        if (declineAll)
        {
            return new DeclineFriendMessage { DeclineAll = declineAll, Friends = [] };
        }

        var playerIds = new List<PlayerId>();
        var count = packet.PopInt();

        while (count > 0)
        {
            playerIds.Add(packet.PopInt());

            count--;
        }

        return new DeclineFriendMessage { DeclineAll = false, Friends = playerIds };
    }
}
