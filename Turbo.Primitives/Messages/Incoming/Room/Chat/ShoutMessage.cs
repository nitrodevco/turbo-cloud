using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Chat;

public sealed record ShoutMessage : IMessageEvent
{
    public required string Text { get; init; }
    public required int StyleId { get; init; }
}
