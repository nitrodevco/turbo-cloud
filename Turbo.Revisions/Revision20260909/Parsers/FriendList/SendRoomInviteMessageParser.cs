using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.FriendList;

public class SendRoomInviteMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var friendIds = packet.PopList(bytesPerItem: 4, p => p.PopInt());
        var message = packet.PopString();

        return new SendRoomInviteMessage { FriendIds = friendIds, Message = message };
    }
}
