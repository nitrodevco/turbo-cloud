using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record RoomInviteMessage : IComposer
{
    public required int SenderId { get; init; }
    public required string Message { get; init; }
}
