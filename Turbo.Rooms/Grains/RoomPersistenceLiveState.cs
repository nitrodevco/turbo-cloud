using System.Collections.Generic;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Commands.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots.Chat;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Rooms.Grains;

/// <summary>What the room has changed since the last flush. Everything here is a write buffer.</summary>
internal sealed class RoomPersistenceLiveState
{
    public required RoomId RoomId { get; init; }

    /// <summary>Swapped for an empty one at the start of a flush, so it is settable.</summary>
    public Dictionary<long, RoomItemSnapshot> DirtyItems { get; set; } = [];

    /// <summary>Changes of who holds a furni, oldest first, kept until each has been written.</summary>
    public Queue<CustodyWrite> PendingCustodyWrites { get; } = new();
    public Dictionary<int, PetSnapshot> DirtyPets { get; } = [];
    public Dictionary<int, BotSnapshot> DirtyBots { get; } = [];
    public Queue<RoomChatlogSnapshot> PendingChatlogs { get; } = new();
    public Queue<CommandLogSnapshot> PendingCommandLogs { get; } = new();
    public Queue<WiredRoomLogEntrySnapshot> PendingWiredLogs { get; } = new();
}

/// <summary>A change of who holds a furni and where, written through rather than buffered.</summary>
internal abstract record CustodyWrite(MovementCause Cause);

/// <summary>An item put down in the room from its owner's inventory.</summary>
internal sealed record CustodyPlacement(RoomItemSnapshot Item, MovementCause Cause)
    : CustodyWrite(Cause);

/// <summary>An item a use took out of the room for good.</summary>
internal sealed record CustodyDestroy(RoomItemSnapshot Item, MovementCause Cause)
    : CustodyWrite(Cause);

/// <summary>Items that left the room for the inventories their snapshots name.</summary>
internal sealed record CustodyPickup(IReadOnlyList<RoomItemSnapshot> Items, MovementCause Cause)
    : CustodyWrite(Cause);

/// <summary>Borrowed furni that left the room, going back to the club.</summary>
internal sealed record CustodyGiveBack(IReadOnlyList<RoomItemSnapshot> Items, MovementCause Cause)
    : CustodyWrite(Cause);
