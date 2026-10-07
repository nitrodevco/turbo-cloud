using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains.WiredTrading;

internal sealed class WiredChestLiveState
{
    public required RoomObjectId ChestId { get; init; }

    public int Coins { get; set; }

    public int CapacityLevel { get; set; }

    /// <summary>The furni stored, oldest first: the order a first-in-first-out give walks.</summary>
    public List<WiredChestStoredItem> Items { get; } = [];

    /// <summary>Players with the chest's window open; they are told every change.</summary>
    public HashSet<PlayerId> ViewerIds { get; } = [];

    /// <summary>
    /// The order a random give takes items in, drawn ahead so the chest can show the next ones
    /// above itself. Items leaving by any other way are dropped from it; new ones go in at a
    /// random place.
    /// </summary>
    public List<RoomObjectId> RandomOrder { get; } = [];
}
