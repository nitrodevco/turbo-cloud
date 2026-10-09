namespace Turbo.Primitives.Catalog.Enums;

/// <summary>What became of credits bought, recorded for the bonus rare.</summary>
public enum BonusRarePurchaseResult
{
    /// <summary>They count toward the reward.</summary>
    Recorded = 0,

    /// <summary>The receipt was recorded before: they count once.</summary>
    AlreadyRecorded = 1,

    /// <summary>No campaign counts bought credits now.</summary>
    NoCampaign = 2,

    /// <summary>No credits, or no such player.</summary>
    Rejected = 3,
}
