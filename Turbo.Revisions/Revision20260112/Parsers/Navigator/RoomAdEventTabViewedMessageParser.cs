using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Navigator;

internal class RoomAdEventTabViewedMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new RoomAdEventTabViewedMessage();
}
