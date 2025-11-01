using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record FlatCreatedMessageComposer : IComposer
{
    public required int RoomId { get; init; }
    public required string Name { get; init; }
}
