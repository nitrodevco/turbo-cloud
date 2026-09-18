using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record RespectUserMessage : IMessageEvent
{
    public required PlayerId PlayerId { get; init; }
}
