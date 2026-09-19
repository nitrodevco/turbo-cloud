namespace Turbo.Primitives.Trading.Enums;

/// <summary>Reason of <c>TradingClose</c>; a commit error is the only one the client alerts on by itself.</summary>
public enum TradeCloseReasonType
{
    Closed = 0,
    CommitError = 1,
}
