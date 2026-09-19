using System;
using System.Collections.Generic;
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
}
