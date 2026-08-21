using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.FriendList;

public class FriendListUpdateMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new FriendListUpdateMessage();
}
