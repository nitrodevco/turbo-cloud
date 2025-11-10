using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record HabboSearchResultMessage : IComposer
{
    public required List<MessengerSearchResultSnapshot> Friends { get; init; }
    public required List<MessengerSearchResultSnapshot> Others { get; init; }
}
