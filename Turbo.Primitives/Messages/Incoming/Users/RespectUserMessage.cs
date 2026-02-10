using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record RespectUserMessage : IMessageEvent
{
    public required int UserId { get; init; }
}
