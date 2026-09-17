using System.Collections.Generic;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;

namespace Turbo.Revisions.Revision20260909.Parsers.FriendList;

public class RemoveFriendMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var friendIds = new List<PlayerId>();
        var count = packet.PopCount(bytesPerItem: 4);

        while (count > 0)
        {
            friendIds.Add(PlayerId.Parse(packet.PopInt()));

            count--;
        }

        return new RemoveFriendMessage { FriendIds = friendIds };
    }
}
