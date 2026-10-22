namespace Turbo.Primitives.Rooms.Enums;

/// <summary>What became of a request to give a room to another player.</summary>
public enum RoomOwnerTransferOutcome
{
    /// <summary>The room is the new owner's.</summary>
    Transferred = 0,

    /// <summary>No room has this id.</summary>
    NoSuchRoom = 1,

    /// <summary>No player has this id.</summary>
    NoSuchPlayer = 2,

    /// <summary>The player already owns the room; nothing changed.</summary>
    AlreadyOwner = 3,

    /// <summary>
    /// The room is a group's home room, and the hotel keeps group home rooms with their owner
    /// (<c>Turbo:Rooms:TransferGroupHomeRooms</c> is off).
    /// </summary>
    GroupHomeRoom = 4,

    /// <summary>The room is being deleted.</summary>
    Deleting = 5,

    /// <summary>The change could not be saved; nothing changed.</summary>
    Failed = 6,
}
