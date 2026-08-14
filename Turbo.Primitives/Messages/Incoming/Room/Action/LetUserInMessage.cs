using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Action;

public record LetUserInMessage : IMessageEvent
{
    public required string Name { get; init; }
    public bool CanEnter { get; init; }
}
