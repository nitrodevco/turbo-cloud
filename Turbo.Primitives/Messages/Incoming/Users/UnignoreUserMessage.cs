using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record UnignoreUserMessage : IMessageEvent
{
    public required int PlayerId { get; init; }
}
