using System;
using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots.Messenger;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record ConsoleMessageHistoryMessageComposer : IComposer
{
    [Id(0)]
    public required int ChatId { get; init; }

    [Id(1)]
    public required List<MessageHistoryEntrySnapshot> Messages { get; init; }

    /// <summary>
    /// When this was built. Serializers read time from here, never from the clock: one
    /// instance is serialized once and its bytes are sent to every recipient.
    /// </summary>
    [Id(2)]
    public DateTime SentAtUtc { get; init; } = DateTime.UtcNow;
}
