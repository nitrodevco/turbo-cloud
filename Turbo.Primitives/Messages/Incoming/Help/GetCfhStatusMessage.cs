using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Help;

public record GetCfhStatusMessage : IMessageEvent
{
    public bool Flag { get; init; }
}
