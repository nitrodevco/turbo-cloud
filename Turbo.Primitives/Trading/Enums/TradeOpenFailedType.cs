namespace Turbo.Primitives.Trading.Enums;

/// <summary>
/// Reason of <c>TradeOpenFailed</c>. The client shows the text
/// <c>inventory.trading.openfail.&lt;reason&gt;</c> with the other user's name, except
/// <see cref="YouAreAlreadyTrading"/> and <see cref="OtherAlreadyTrading"/>, which both get its
/// "trade already open" popup (Flash <c>TradingModel.handleMessageEvent</c>).
/// </summary>
public enum TradeOpenFailedType
{
    /// <summary>"Trading is not currently allowed in Habbo Hotel."</summary>
    HotelDisabled = 1,

    /// <summary>"Trading is currently disabled for your account."</summary>
    AccountDisabled = 2,

    /// <summary>"You can't trade because you have a trade lock."</summary>
    YouAreTradeLocked = 3,

    /// <summary>"%otherusername% doesn't want to / cannot trade atm."</summary>
    OtherCannotTrade = 4,

    /// <summary>"Trading is not allowed in this room."</summary>
    RoomDisallows = 6,

    /// <summary>"You already have an ongoing trade."</summary>
    YouAreAlreadyTrading = 7,

    /// <summary>"%otherusername% is already trading."</summary>
    OtherAlreadyTrading = 8,
}
