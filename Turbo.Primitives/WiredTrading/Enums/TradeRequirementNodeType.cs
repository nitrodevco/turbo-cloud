namespace Turbo.Primitives.WiredTrading.Enums;

/// <summary>One entry of a trade rule: an amount of coins, or an amount of one furni type.</summary>
public enum TradeRequirementNodeType : byte
{
    Coin = 0,
    Furni = 1,
}
