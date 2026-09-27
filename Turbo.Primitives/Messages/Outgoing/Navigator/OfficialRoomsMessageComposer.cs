using System;
using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

/// <summary>The legacy official rooms list; each room is sent as a guest-room entry.</summary>
[GenerateSerializer, Immutable]
public sealed record OfficialRoomsMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<RoomInfoSnapshot> Rooms { get; init; }

    /// <summary>
    /// When this was built. Serializers read time from here, never from the clock: one
    /// instance is serialized once and its bytes are sent to every recipient.
    /// </summary>
    [Id(1)]
    public DateTime SentAtUtc { get; init; } = DateTime.UtcNow;
}
