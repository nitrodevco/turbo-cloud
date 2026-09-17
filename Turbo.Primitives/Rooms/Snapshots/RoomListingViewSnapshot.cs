using System;
using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots;

/// <summary>
/// What a navigator cache needs from the room directory in one call: live info for active rooms
/// and the listing keys changed since the caller's last position.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomListingViewSnapshot
{
    [Id(0)]
    public required ImmutableArray<RoomActiveSnapshot> ActiveRooms { get; init; }

    /// <summary>Identifies this directory activation; sequences restart when it changes.</summary>
    [Id(1)]
    public required Guid Epoch { get; init; }

    [Id(2)]
    public required long Sequence { get; init; }

    [Id(3)]
    public required ImmutableArray<string> ChangedKeys { get; init; }

    /// <summary>
    /// The caller's position is unknown or too old for the change log, so it must drop every
    /// cached listing.
    /// </summary>
    [Id(4)]
    public required bool IsReset { get; init; }
}
