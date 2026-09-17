using System;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record RoomEventMessageComposer : IComposer
{
    [Id(0)]
    public required RoomEventSnapshot Event { get; init; }

    /// <summary>The time the relative minute fields are computed against.</summary>
    [Id(1)]
    public required DateTime SentAtUtc { get; init; }
}
