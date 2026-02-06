using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record GetExtendedProfileByNameMessage : IMessageEvent
{
    public required string UserName { get; init; }
}
