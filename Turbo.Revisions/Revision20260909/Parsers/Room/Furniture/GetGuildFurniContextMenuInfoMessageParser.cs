using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Furniture;

internal class GetGuildFurniContextMenuInfoMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetGuildFurniContextMenuInfoMessage
        {
            ObjectId = RoomObjectId.Parse(packet.PopInt()),
            Category = packet.PopInt(),
        };
}
