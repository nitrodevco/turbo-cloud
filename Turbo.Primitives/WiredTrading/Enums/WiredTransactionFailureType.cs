namespace Turbo.Primitives.WiredTrading.Enums;

/// <summary>Why a wired trade or transaction failed; the client shows <c>wired_transactions.notification.fail.{id}</c>.</summary>
public enum WiredTransactionFailureType
{
    UserCancelled = 0,
    Invalid = 1,
    Timeout = 2,
    TradeCancelled = 3,
    AlreadyTrading = 4,
    Misconfig = 5,
    InsufficientFunds = 6,

    /// <summary>The funds were there when the trade started and are no longer.</summary>
    FundsGone = 7,
    UserCantTrade = 8,
    OwnerCantTrade = 9,
    Empty = 10,
    ChestFull = 11,
    Disabled = 12,
    ChestNotInRoom = 13,
    TooManyChests = 14,
    NoOrLockedChests = 15,
    CantGiveAllToMultipleUsers = 16,
    TooManyOffers = 17,
    TooFast = 18,
    ExceedsCapacity = 19,
    InternalError = 1000,
    DatabaseError = 1001,

    /// <summary>A database error that left the room needing a reload.</summary>
    DatabaseErrorReloadRequired = 1002,
}
