using System.Collections.Generic;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.FriendList;

public class SendRoomInviteMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var friendIds = new List<int>();

        // The message after the ids needs at least two bytes of its own.
        var totalInvites = packet.PopCount(bytesPerItem: 4);

        for (var i = 0; i < totalInvites; i++)
        {
            friendIds.Add(packet.PopInt());
        }

        var message = packet.PopString();

        return new SendRoomInviteMessage { FriendIds = friendIds, Message = message };
    }
}
