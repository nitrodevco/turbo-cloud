using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.RoomSettings;

internal class GetBannedUsersFromRoomMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetBannedUsersFromRoomMessage { RoomId = packet.PopInt() };
}
