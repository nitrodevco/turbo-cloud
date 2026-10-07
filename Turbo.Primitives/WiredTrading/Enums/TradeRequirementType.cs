namespace Turbo.Primitives.WiredTrading.Enums;

/// <summary>What a wired trade asks the user to give; only <see cref="Rules"/> carries a rule set.</summary>
public enum TradeRequirementType
{
    AnyCoins = 0,
    AnyFurni = 1,
    Anything = 2,
    Rules = 4,
}
