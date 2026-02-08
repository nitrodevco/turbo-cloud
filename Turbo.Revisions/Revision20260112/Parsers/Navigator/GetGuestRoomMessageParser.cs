using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Navigator;

internal class GetGuestRoomMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetGuestRoomMessage
        {
            RoomId = packet.PopInt(),
            EnterRoom = packet.PopInt() == 1,
            RoomForward = packet.PopInt() == 1,
        };
}
