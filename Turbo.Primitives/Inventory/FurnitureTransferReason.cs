namespace Turbo.Primitives.Inventory;

/// <summary>Why furniture changed owner, as <c>furniture_transfers</c> records it.</summary>
public enum FurnitureTransferReason : byte
{
    /// <summary>One side of a completed trade.</summary>
    Trade = 0,

    /// <summary>A trade's first side handed back because its second side could not move.</summary>
    TradeUndone = 1,

    /// <summary>
    /// Picked up from a room by a staff member who keeps what they pick up
    /// (<c>room.furni.steal</c>), rather than sending it to its owner.
    /// </summary>
    StaffPickup = 2,

    /// <summary>Deposited by one player in a wired chest another owns.</summary>
    ChestDeposit = 3,

    /// <summary>Paid out of a wired chest to a player other than its owner.</summary>
    ChestWithdrawal = 4,

    /// <summary>
    /// Taken out of a present by whoever opened it, when that is not who it was wrapped for:
    /// the present changed hands while it was wrapped.
    /// </summary>
    PresentOpened = 5,
}
