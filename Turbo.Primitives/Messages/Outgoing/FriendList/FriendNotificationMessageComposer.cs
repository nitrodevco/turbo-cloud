using Orleans;
using Turbo.Primitives.FriendList.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record FriendNotificationMessageComposer : IComposer
{
    [Id(0)]
    public required string AvatarId { get; init; }

    [Id(1)]
    public required FriendNotificationCodeType TypeCode { get; init; }

    [Id(2)]
    public required string Message { get; init; }
}
