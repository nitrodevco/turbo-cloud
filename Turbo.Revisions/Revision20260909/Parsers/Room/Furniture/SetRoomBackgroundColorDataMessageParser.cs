using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Furniture;

internal class SetRoomBackgroundColorDataMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SetRoomBackgroundColorDataMessage
        {
            ObjectId = packet.PopInt(),
            Hue = packet.PopInt(),
            Saturation = packet.PopInt(),
            Lightness = packet.PopInt(),
        };
}
