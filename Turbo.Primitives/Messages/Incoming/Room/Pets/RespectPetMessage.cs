using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Pets;

public record RespectPetMessage : IMessageEvent
{
    public required int PetId { get; init; }
}
