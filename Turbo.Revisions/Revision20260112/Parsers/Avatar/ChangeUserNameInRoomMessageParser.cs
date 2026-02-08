using Turbo.Primitives.Messages.Incoming.Avatar;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Avatar;

internal class ChangeUserNameInRoomMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new ChangeUserNameInRoomMessage();
}
