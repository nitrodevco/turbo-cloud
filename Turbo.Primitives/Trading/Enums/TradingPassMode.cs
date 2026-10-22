namespace Turbo.Primitives.Trading.Enums;

/// <summary>What a player without the trading pass may do in a trade and on the marketplace.</summary>
public enum TradingPassMode
{
    /// <summary>
    /// Habbo's rule: no trade opens unless both players hold the pass, and nobody without it
    /// sells on the marketplace.
    /// </summary>
    Required,

    /// <summary>
    /// The trade opens, but a player without the pass can only receive: the client shows the
    /// trade window's "does not have trading in use" warnings and hides their offer button, and
    /// the server refuses their offers. Nobody without it sells on the marketplace.
    /// </summary>
    ReceiveOnly,

    /// <summary>No pass is asked for: everyone trades and sells.</summary>
    Off,
}
