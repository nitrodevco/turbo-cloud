using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Furniture;

internal class AddSpamWallPostItMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new AddSpamWallPostItMessage
        {
            ObjectId = packet.PopInt(),
            Location = packet.PopString(),
            Color = packet.PopString(),
            Text = packet.PopString(),
        };
}
