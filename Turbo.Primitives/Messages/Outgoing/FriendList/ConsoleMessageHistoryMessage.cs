using System.Collections.Generic;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record ConsoleMessageHistoryMessage : IComposer
{
    public required int ChatId { get; init; }
    public required List<MessageHistoryEntrySnapshot> Messages { get; init; }
}
