using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

public sealed record YouAreSpectatorMessageComposer : IComposer
{
    public required int RoomId { get; init; }
}
