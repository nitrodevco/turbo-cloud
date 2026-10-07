using System.Collections.Generic;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Rooms.Grains.WiredTrading;

/// <summary>
/// One trade with wired: a deposit into a chest, or a contract's payment or trade. What is
/// offered, and whether it was accepted.
/// </summary>
internal sealed class WiredTradeSession
{
    public required RoomId RoomId { get; init; }

    /// <summary>The chest a deposit goes into; the contract or box for a contract's trade.</summary>
    public required RoomObjectId TargetId { get; init; }

    /// <summary>What a deposit takes; unused for a contract's trade.</summary>
    public WiredChestKind Kind { get; init; }

    /// <summary>The contract's trade; null for a deposit.</summary>
    public WiredContractTradeRequest? Contract { get; init; }

    /// <summary>The offered items as the inventory held them when offered, in offer order.</summary>
    public List<FurnitureItemSnapshot> Offer { get; } = [];

    /// <summary>The first press; the trade goes through on the second, and any change undoes it.</summary>
    public bool IsAccepted { get; set; }

    /// <summary>
    /// Wired cancelled it; it ends when the grace period runs out, unless the same stack opens
    /// a new trade first, which then takes its place without the window closing.
    /// </summary>
    public bool IsCancelPending { get; set; }
}
