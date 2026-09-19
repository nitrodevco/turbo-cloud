using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Engine;

internal class PlacePetMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new PlacePetMessage
        {
            PetId = packet.PopInt(),
            X = packet.PopInt(),
            Y = packet.PopInt(),
        };
}
