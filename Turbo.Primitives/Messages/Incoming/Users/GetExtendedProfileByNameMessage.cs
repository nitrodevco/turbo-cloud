using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record GetExtendedProfileByNameMessage : IMessageEvent
{
    public string UserName { get; init; } = string.Empty;
}
