using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Grains;

/// <summary>The directory is one grain for the hotel, so its state carries no key.</summary>
internal sealed class RoomDirectoryLiveState
{
    public Dictionary<RoomId, RoomInfoSnapshot> ActiveRooms { get; } = [];

    /// <summary>
    /// How each room looked when it became active, so a listing it has since left (for example
    /// an old category) is also invalidated when it deactivates.
    /// </summary>
    public Dictionary<RoomId, RoomInfoSnapshot> ActivatedRooms { get; } = [];
    public Queue<(long Sequence, string Key)> ListingChanges { get; } = new();
    public Guid ListingEpoch { get; } = Guid.NewGuid();
    public long ListingSequence { get; set; }
    public Dictionary<RoomId, List<PlayerId>> RoomPlayers { get; } = [];
    public Dictionary<RoomId, int> RoomPopulations { get; } = [];

    /// <summary>
    /// Every active room with its population, as the navigator's listing view hands it out.
    /// Derived from <see cref="ActiveRooms"/> and <see cref="RoomPopulations"/>: cleared whenever
    /// either changes and rebuilt by the next request, because building it on every request
    /// copied every active room inside this hotel-wide grain's turn.
    /// </summary>
    public ImmutableArray<RoomActiveSnapshot>? ActiveRoomsView { get; set; }
}
