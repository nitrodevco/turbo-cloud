using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record RateFlatMessage : IMessageEvent
{
    public int Points { get; init; }
}
