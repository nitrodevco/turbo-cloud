using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record ScrGetUserInfoMessage : IMessageEvent
{
    public required string ProductName { get; init; }
}
