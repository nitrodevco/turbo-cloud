using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public sealed record RoomInfoUpdatedMessageComposer : IComposer
{
    public required int RoomId { get; init; }
}
