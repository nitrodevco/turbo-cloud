namespace Turbo.Primitives.Catalog.Enums;

/// <summary>
/// Error codes for LTD raffle entry failures.
/// </summary>
public enum LtdRaffleEntryError
{
    None = 0,
    SeriesNotFound = 1,
    SeriesNotActive = 2,
    SoldOut = 3,
    AlreadyInQueue = 4,
    InsufficientFunds = 5,
    RaffleProcessing = 6,
    AlreadyWon = 7,
}
