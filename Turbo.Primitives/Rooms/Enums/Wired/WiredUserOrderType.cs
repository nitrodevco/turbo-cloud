namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>The order a user selector lists the users it picked in.</summary>
public enum WiredUserOrderType
{
    /// <summary>The room's one user order, shared by every user selector: ascending room index, bots included.</summary>
    RoomIndex = 0,

    /// <summary>The order the selector found them in, kept as it is.</summary>
    AsSelected = 1,
}
