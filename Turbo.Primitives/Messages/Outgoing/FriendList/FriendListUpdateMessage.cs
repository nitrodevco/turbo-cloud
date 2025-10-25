using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public record FriendListUpdateMessage : IComposer
{
    public required List<FriendCategorySnapshot> FriendCategories { get; init; }
    public required List<FriendListUpdateSnapshot> Updates { get; init; }
}
