using Turbo.Primitives.Messages.Incoming.Room.Pets;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Pets.Enums;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Pets;

internal class BreedPetsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new BreedPetsMessage
        {
            Action = (PetBreedingAction)packet.PopInt(),
            PetId = packet.PopInt(),
            OtherPetId = packet.PopInt(),
        };
}
