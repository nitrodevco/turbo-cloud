using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public record AcceptFriendResultMessage : IComposer
{
    public required List<AcceptFriendFailureSnapshot> Failures { get; init; }
}
