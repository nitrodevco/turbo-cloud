namespace Turbo.Primitives.WiredTrading.Enums;

/// <summary>How a wired trade's rule set scales: once, by a fixed multiplier, or as often as the user can pay up to a cap.</summary>
public enum TradeRequirementRulesType
{
    Single = 0,
    Multiplier = 1,
    AutoMultiplier = 2,
}
