namespace Turbo.Primitives.Trading.Enums;

/// <summary>
/// Where a trade stands: offers are edited while <see cref="Open"/>; once both sides accept it
/// is <see cref="Confirming"/> and each side must confirm again (the client counts down) before
/// the items move.
/// </summary>
public enum TradeStateType
{
    Open,
    Confirming,
    Completed,
}
