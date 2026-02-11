namespace Turbo.Primitives.Catalog.Enums;

/// <summary>
/// Result codes for LTD raffle outcomes.
/// </summary>
public enum LtdRaffleResultCode : byte
{
    /// <summary>
    /// Player won the raffle and received the item.
    /// Triggers notification.raffle.won in client.
    /// </summary>
    Won = 0,

    /// <summary>
    /// Player lost the raffle.
    /// Triggers notification.raffle.lost in client.
    /// </summary>
    Lost = 1,

    /// <summary>
    /// Player lost because stock ran out during raffle.
    /// </summary>
    LostNoStock = 2,

    /// <summary>
    /// Player lost due to an error.
    /// </summary>
    LostError = 3,
}
