using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

public sealed record RoomReadyMessageComposer : IComposer
{
    public required string WorldType { get; init; }
    public required int RoomId { get; init; }
}
