using Turbo.Contracts.Abstractions;
using Turbo.Primitives.FriendList.Enums;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record FriendNotificationMessage : IComposer
{
    public required string AvatarId { get; init; }
    public required FriendNotificationCodeType TypeCode { get; init; }
    public required string Message { get; init; }
}
