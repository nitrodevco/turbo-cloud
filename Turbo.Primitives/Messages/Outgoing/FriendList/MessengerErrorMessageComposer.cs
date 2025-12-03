using Orleans;
using Turbo.Primitives.FriendList.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record MessengerErrorMessageComposer : IComposer
{
    [Id(0)]
    public required int ClientMessageId { get; init; }

    [Id(1)]
    public required FriendListErrorCodeType ErrorCode { get; init; }
}
