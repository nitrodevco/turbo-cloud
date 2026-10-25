namespace Turbo.Primitives.Trading.Enums;

/// <summary>
/// What a player may do in a trade, as <see cref="IPlayerTradePolicy"/> decides it. A trade asks
/// it of both players before it opens; wired chests ask it before they move anything.
/// </summary>
public enum TradeStandingType
{
    /// <summary>
    /// The account may not trade (a trade lock): starting a trade answers
    /// <c>inventory.trading.openfail.3</c>, asking them answers <c>openfail.4</c>.
    /// </summary>
    Locked,

    /// <summary>
    /// The account lacks the trading pass the hotel asks for: starting a trade shows
    /// <c>trade.trading_perk</c>, asking them answers <c>openfail.4</c>.
    /// </summary>
    NoPass,

    /// <summary>The trade opens, but the player can only receive: they offer nothing.</summary>
    ReceiveOnly,

    /// <summary>The player trades both ways.</summary>
    Full,
}
