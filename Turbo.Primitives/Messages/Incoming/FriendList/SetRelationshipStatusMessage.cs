using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record SetRelationshipStatusMessage : IMessageEvent
{
    public required int FriendUserId { get; init; }
    public required int RelationType { get; init; }
}
