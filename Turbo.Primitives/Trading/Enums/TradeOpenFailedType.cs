namespace Turbo.Primitives.Trading.Enums;

/// <summary>
/// Reason of <c>TradeOpenFailed</c>. The client shows the text
/// <c>inventory.trading.openfail.&lt;reason&gt;</c> with the other user's name, except
/// <see cref="AlreadyOpen"/> which is its "trade already open" popup.
/// </summary>
public enum TradeOpenFailedType
{
    YouAreNotAllowed = 1,
    OtherNotAllowed = 2,
    OtherAlreadyTrading = 3,
    YouAreAlreadyTrading = 4,
    AlreadyOpen = 7,
}
