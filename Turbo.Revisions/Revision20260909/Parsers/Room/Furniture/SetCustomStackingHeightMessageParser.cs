using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Furniture;

internal class SetCustomStackingHeightMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var objectId = packet.PopInt();
        var height = packet.PopInt();

        // Only walk-magic tiles append the multi-walk flag.
        bool? multiWalk = packet.End ? null : packet.PopBoolean();

        return new SetCustomStackingHeightMessage
        {
            ObjectId = objectId,
            Height = height,
            MultiWalk = multiWalk,
        };
    }
}
