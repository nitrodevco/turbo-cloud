namespace Turbo.Achievements.Configuration;

public sealed class RewardTrackTaskLevelDefinition
{
    public int RequiredCount { get; set; }

    public int Points { get; set; }

    /// <summary>Gives its points only to a player who owns the track's premium.</summary>
    public bool Premium { get; set; }
}
