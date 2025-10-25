using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public record FriendNotificationMessage : IComposer
{
    public required string AvatarId { get; init; }
    public required FriendNotificationCodeEnum TypeCode { get; init; }
    public required string Message { get; init; }
}
