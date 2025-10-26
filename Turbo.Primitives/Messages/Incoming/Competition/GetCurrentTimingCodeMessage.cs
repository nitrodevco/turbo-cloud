using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Competition;

public record GetCurrentTimingCodeMessage : IMessageEvent
{
    public required string SlotConfig { get; init; }
}
