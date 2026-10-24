namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>The order a user selector lists the users it picked in.</summary>
public enum WiredUserOrderType
{
    /// <summary>The user who entered or re-entered the room last first.</summary>
    NewestFirst = 0,

    /// <summary>Ascending room index, bots included.</summary>
    RoomIndex = 1,

    /// <summary>The order the selector found them in, kept as it is.</summary>
    AsSelected = 2,
}
