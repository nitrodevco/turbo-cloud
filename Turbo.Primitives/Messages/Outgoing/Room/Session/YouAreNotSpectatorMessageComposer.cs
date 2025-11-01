using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

public record YouAreNotSpectatorMessageComposer : IComposer
{
    public required int RoomId { get; init; }
}
