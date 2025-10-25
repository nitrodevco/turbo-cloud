using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.Friendlist;

public record HabboSearchResultMessage : IComposer
{
    public required List<MessengerSearchResultSnapshot> Friends { get; init; }
    public required List<MessengerSearchResultSnapshot> Others { get; init; }
}
