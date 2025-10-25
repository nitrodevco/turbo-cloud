using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public record RoomInviteMessage : IComposer
{
    public required int SenderId { get; init; }
    public required string Message { get; init; }
}
