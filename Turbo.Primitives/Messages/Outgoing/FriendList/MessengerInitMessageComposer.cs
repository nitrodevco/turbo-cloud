using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public record MessengerInitMessageComposer : IComposer
{
    public required int UserFriendLimit { get; init; }
    public required int NormalFriendLimit { get; init; }
    public required int ExtendedFriendLimit { get; init; }
    public required List<FriendCategorySnapshot> FriendCategories { get; init; }
}
