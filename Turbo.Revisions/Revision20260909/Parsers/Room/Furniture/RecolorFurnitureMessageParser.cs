using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Furniture;

internal class RecolorFurnitureMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new RecolorFurnitureMessage
        {
            ObjectId = packet.PopInt(),
            Scope = (RecolorScopeType)packet.PopInt(),
            Red = packet.PopInt(),
            Green = packet.PopInt(),
            Blue = packet.PopInt(),
        };
}
