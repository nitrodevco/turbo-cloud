using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Avatar;

public record CheckUserNameMessage : IMessageEvent
{
    public required string Name { get; init; }
}
