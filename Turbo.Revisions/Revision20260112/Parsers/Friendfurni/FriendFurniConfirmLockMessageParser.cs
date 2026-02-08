using Turbo.Primitives.Messages.Incoming.Friendfurni;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Friendfurni;

internal class FriendFurniConfirmLockMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new FriendFurniConfirmLockMessage();
}
