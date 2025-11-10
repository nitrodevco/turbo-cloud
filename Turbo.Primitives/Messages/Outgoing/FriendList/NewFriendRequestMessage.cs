using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record NewFriendRequestMessage : IComposer
{
    public required FriendRequestSnapshot Request { get; init; }
}
