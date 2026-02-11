using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record FindFriendsProcessResultMessageComposer : IComposer
{
    [Id(0)]
    public required bool Success { get; init; }
}
