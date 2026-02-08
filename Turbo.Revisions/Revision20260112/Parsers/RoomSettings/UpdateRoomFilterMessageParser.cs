using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.RoomSettings;

internal class UpdateRoomFilterMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new UpdateRoomFilterMessage
        {
            RoomId = packet.PopInt(),
            IsAddingWord = packet.PopBoolean(),
            Word = packet.PopString(),
        };
}
