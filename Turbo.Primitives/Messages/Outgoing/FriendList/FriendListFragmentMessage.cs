using System.Collections.Generic;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record FriendListFragmentMessage : IComposer
{
    public required int TotalFragments { get; init; }
    public required int FragmentIndex { get; init; }
    public required List<MessengerFriendSnapshot> Fragment { get; init; }
}
