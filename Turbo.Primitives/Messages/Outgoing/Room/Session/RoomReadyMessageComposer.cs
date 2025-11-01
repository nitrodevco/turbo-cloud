using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

public record RoomReadyMessageComposer : IComposer
{
    public required string RoomType { get; init; }
    public required int RoomId { get; init; }
}
