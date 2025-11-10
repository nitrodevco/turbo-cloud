using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

public sealed record FlatAccessibleMessageComposer : IComposer
{
    public required int RoomId { get; init; }
    public required string Username { get; init; }
}
