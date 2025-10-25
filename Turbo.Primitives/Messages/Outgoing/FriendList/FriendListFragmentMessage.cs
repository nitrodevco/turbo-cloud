using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public record FriendListFragmentMessage : IComposer
{
    public required int TotalFragments { get; init; }
    public required int FragmentIndex { get; init; }
    public required List<MessengerFriendSnapshot> Fragment { get; init; }
}
