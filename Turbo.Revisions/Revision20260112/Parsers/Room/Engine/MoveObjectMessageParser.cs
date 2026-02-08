using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260112.Parsers.Room.Engine;

internal class MoveObjectMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new MoveObjectMessage
        {
            ObjectId = packet.PopInt(),
            X = packet.PopInt(),
            Y = packet.PopInt(),
            Rotation = (Rotation)packet.PopInt(),
        };
}
