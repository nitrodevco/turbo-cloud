using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;

namespace Turbo.Revisions.Revision20260909.Parsers.FriendList;

public class DeclineFriendMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var declineAll = packet.PopBoolean();

        if (declineAll)
        {
            return new DeclineFriendMessage { DeclineAll = declineAll, Friends = [] };
        }

        var playerIds = packet.PopList<PlayerId>(bytesPerItem: 4, p => p.PopInt());

        return new DeclineFriendMessage { DeclineAll = false, Friends = playerIds };
    }
}
