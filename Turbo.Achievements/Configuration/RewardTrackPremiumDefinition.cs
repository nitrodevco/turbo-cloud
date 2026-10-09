namespace Turbo.Achievements.Configuration;

public sealed class RewardTrackPremiumDefinition
{
    /// <summary>What task points are multiplied by for a premium owner (1.5 shows "50% faster").</summary>
    public double TaskPointsBoost { get; set; } = 1;

    /// <summary>Points given at purchase.</summary>
    public int InstantPoints { get; set; }

    public int CostCredits { get; set; }

    public int CostDiamonds { get; set; }
}
