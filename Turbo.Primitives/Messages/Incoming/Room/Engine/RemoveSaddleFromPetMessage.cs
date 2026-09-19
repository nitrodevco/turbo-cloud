using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record RemoveSaddleFromPetMessage : IMessageEvent
{
    public required int PetId { get; init; }
}
