using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Pets;

public record PetSelectedMessage : IMessageEvent
{
    public required int PetId { get; init; }
}
