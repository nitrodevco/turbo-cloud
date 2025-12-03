using Orleans;
using Turbo.Primitives.FriendList.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record InstantMessageErrorMessageComposer : IComposer
{
    [Id(0)]
    public required InstantMessageErrorCodeType ErrorCode { get; init; }

    [Id(1)]
    public required long PlayerId { get; init; }

    [Id(2)]
    public required string Message { get; init; }
}
