using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record ClickCharacterMessage : IMessageEvent
{
    public required int UserId { get; init; }
}
