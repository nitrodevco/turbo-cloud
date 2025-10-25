using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.Friendlist;

public record FriendListFragmentMessage : IComposer
{
    public required List<List<MessengerFriendSnapshot>> FriendListFragments { get; init; }
}
