using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Avatar;

public record PassCarryItemToPetMessage : IMessageEvent
{
    public required int PetId { get; init; }
}
