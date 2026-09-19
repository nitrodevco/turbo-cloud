using Turbo.Primitives.Messages.Incoming.Inventory.Pets;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Inventory.Pets;

internal class ConfirmPetBreedingMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new ConfirmPetBreedingMessage
        {
            NestId = packet.PopInt(),
            Name = packet.PopString(),
            PetId = packet.PopInt(),
            OtherPetId = packet.PopInt(),
        };
}
