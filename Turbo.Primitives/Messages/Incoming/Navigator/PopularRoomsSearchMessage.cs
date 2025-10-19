using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record PopularRoomsSearchMessage : IMessageEvent
{
    public required string Query { get; init; }
    public int AdIndex { get; init; }
}
