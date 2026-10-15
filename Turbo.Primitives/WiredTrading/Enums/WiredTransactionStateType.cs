namespace Turbo.Primitives.WiredTrading.Enums;

/// <summary>
/// Where a player's open transaction is, as <c>@transaction.state</c> reads it: Habbo labels 1
/// "Adding Items" and 2 "Confirmed" (official client capture, WIN63-202610121709).
/// </summary>
public enum WiredTransactionStateType
{
    AddingItems = 1,
    Confirmed = 2,
}
