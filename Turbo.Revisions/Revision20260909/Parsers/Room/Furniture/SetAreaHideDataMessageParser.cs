using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Furniture;

internal class SetAreaHideDataMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SetAreaHideDataMessage
        {
            ObjectId = packet.PopInt(),
            RootX = packet.PopInt(),
            RootY = packet.PopInt(),
            Width = packet.PopInt(),
            Length = packet.PopInt(),
            Invisibility = packet.PopBoolean(),
            WallItems = packet.PopBoolean(),
            Invert = packet.PopBoolean(),
        };
}
