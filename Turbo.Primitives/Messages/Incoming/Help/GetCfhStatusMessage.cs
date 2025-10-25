using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record GetCfhStatusMessage : IMessageEvent
{
    public bool Flag { get; init; }
}
