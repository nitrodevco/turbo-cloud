using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.Friendlist;

public record ConsoleMessageHistoryMessage : IComposer
{
    public required int ChatId { get; init; }
    public required List<MessageHistoryEntrySnapshot> Messages { get; init; }
}
