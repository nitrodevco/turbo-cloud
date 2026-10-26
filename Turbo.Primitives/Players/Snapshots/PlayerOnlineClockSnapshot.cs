using System;
using Orleans;

namespace Turbo.Primitives.Players.Snapshots;

/// <summary>
/// A player's session as their presence keeps it: when it began, and since when its time online
/// is not yet added to their saved total. Both null while they are offline.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PlayerOnlineClockSnapshot
{
    [Id(0)]
    public DateTime? SessionStartedUtc { get; init; }

    [Id(1)]
    public DateTime? UnsavedSinceUtc { get; init; }
}
