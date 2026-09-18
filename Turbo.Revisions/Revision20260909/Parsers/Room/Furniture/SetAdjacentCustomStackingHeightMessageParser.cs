using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Furniture;

internal class SetAdjacentCustomStackingHeightMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SetAdjacentCustomStackingHeightMessage
        {
            ObjectId = packet.PopInt(),
            Down = packet.PopBoolean(),
        };
}
