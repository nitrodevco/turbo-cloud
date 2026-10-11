namespace Turbo.Primitives.Players.Wallet;

/// <summary>
/// Why a balance moved, as the currency ledger records it. The sign of the change says which way:
/// staff can give or take, a wired chest can take a deposit or pay one out.
/// </summary>
public enum WalletChangeReason : byte
{
    /// <summary>Something bought in the catalog: an offer, a gift, a club membership, an LTD.</summary>
    CatalogPurchase = 0,

    /// <summary>A charge given back because what it paid for could not be made.</summary>
    Refund = 1,

    Voucher = 2,
    Achievement = 3,
    Quest = 4,
    DailyTask = 5,
    RewardTrack = 6,

    /// <summary>Given or taken by staff, with a command or the panel.</summary>
    Staff = 7,

    GroupCreation = 8,

    /// <summary>A photo bought or published.</summary>
    Camera = 9,

    RentableSpace = 10,

    /// <summary>A prize from cracking a crackable furni.</summary>
    Crackable = 11,

    /// <summary>A credit furni (a gold bar, a coin) redeemed.</summary>
    Exchange = 12,

    /// <summary>Credits put into or taken out of a wired chest.</summary>
    WiredChest = 13,

    /// <summary>A credit applied once by its reference, from a plugin or another service.</summary>
    Referenced = 14,
}
