using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record EditEventMessage : IMessageEvent
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
}
