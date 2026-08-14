namespace Turbo.Primitives.Catalog.Enums;

public enum CatalogPurchaseErrorType
{
    None = 0,

    // Mapped to catalog.alert.purchaseerror.description.{ID} (Packet 930)
    BadgeOwned = 1,
    NotEnoughCredits = 2,
    TradeLocked = 3,
    NotEnoughActivityPoints = 4,
    EffectOwned = 5,
    LtdPurchasesLimited = 6,
    GroupRequired = 7,
    SafetyLocked = 8,
    InvalidGiftMessage = 9,
    InventoryFull = 10,
    ChatbubbleOwned = 11,
    RaffleOngoing = 12,
    BlockedByReceiver = 13,
    ReceiverNotFound = 14,

    // Static Description Mapping (1872 Packet)
    RequiresHabboClub = 101, // Handled as case 1 in client
    OfferNotFound = 102,
    OfferMisconfigured = 103,
    PurchaseFailed = 104,
}
