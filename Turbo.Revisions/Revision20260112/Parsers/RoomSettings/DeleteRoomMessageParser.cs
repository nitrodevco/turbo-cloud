using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.RoomSettings;

internal class DeleteRoomMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new DeleteRoomMessage { RoomId = packet.PopInt() };
}
