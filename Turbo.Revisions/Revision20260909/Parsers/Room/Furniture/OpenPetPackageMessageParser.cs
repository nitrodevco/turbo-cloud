using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Furniture;

internal class OpenPetPackageMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new OpenPetPackageMessage { ObjectId = packet.PopInt(), Name = packet.PopString() };
}
