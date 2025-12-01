using System.Collections.Generic;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record HabboSearchResultMessage : IComposer
{
    public required List<MessengerSearchResultSnapshot> Friends { get; init; }
    public required List<MessengerSearchResultSnapshot> Others { get; init; }
}
