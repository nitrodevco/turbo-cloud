using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Navigator;

internal class UpdateHomeRoomMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new UpdateHomeRoomMessage { RoomId = packet.PopInt() };
}
