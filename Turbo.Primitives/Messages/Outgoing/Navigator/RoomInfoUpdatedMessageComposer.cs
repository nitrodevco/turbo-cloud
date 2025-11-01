using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record RoomInfoUpdatedMessageComposer : IComposer
{
    public required int RoomId { get; init; }
}
