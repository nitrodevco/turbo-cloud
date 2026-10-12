using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Avatar;

public record ChangeUserNameMessage : IMessageEvent
{
    public required string Name { get; init; }
}
