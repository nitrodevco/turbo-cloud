namespace Turbo.Primitives.WiredTrading.Enums;

/// <summary>
/// Why an offer in a trade with wired was not taken; the trade stays open
/// (<c>wired_transactions.notification.trade_error.*</c>).
/// </summary>
public enum WiredTradeErrorType
{
    InvalidItem = 0,
    TooManyItems = 1,
    TooManyItemTypes = 2,
    TooManyCredits = 3,
    RequirementsAlreadyMet = 4,
    ExceedsOfferRequirements = 5,
    OutOfFunds = 6,
    TooManyItemsWired = 7,
    TooManyCreditsWired = 8,
    ExceedsChestCapacity = 9,
    ChestNotFound = 10,
}
