using System;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record RoomEventMessageComposer : IComposer
{
    /// <summary>
    /// The room's event, or null for none: Habbo sends that on entering a room without an
    /// event (ids -1), and the client drops the event card of the room before.
    /// </summary>
    [Id(0)]
    public required RoomEventSnapshot? Event { get; init; }

    /// <summary>The time the relative minute fields are computed against.</summary>
    [Id(1)]
    public required DateTime SentAtUtc { get; init; }
}
