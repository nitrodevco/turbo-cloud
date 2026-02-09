using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Avatar;

public record ChangeMottoMessage : IMessageEvent
{
    public required string Text { get; init; }
}
