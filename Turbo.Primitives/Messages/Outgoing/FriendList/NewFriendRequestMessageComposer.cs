using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record NewFriendRequestMessageComposer : IComposer
{
    [Id(0)]
    public required MessengerRequestDto Request { get; init; }
}
