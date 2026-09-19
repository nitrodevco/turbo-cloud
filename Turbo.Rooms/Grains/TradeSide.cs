using System.Collections.Generic;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains;

/// <summary>What one party has put into a trade and how far they have agreed to it.</summary>
public sealed class TradeSide
{
    public Dictionary<RoomObjectId, FurnitureItemSnapshot> Items { get; } = [];
    public bool Accepted { get; set; }
    public bool Confirmed { get; set; }
}
