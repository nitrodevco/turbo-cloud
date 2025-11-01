using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

public record YouAreSpectatorMessageComposer : IComposer
{
    public required int RoomId { get; init; }
}
