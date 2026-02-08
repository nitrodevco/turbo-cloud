using System;

namespace Turbo.Catalog.Configuration;

public class CatalogConfig
{
    public const string SECTION_NAME = "Turbo:Catalog";

    /// <summary>
    /// Configuration for LTD raffle weighting criteria.
    /// </summary>
    public LtdRaffleWeightConfig LtdRaffle { get; set; } = new();
}

/// <summary>
/// Configuration for LTD raffle weighting criteria.
/// Hotel owners can tune these values to define their own fairness criteria.
/// </summary>
public class LtdRaffleWeightConfig
{
    /// <summary>
    /// Base weight given to all participants (ensures everyone has a chance).
    /// </summary>
    public double BaseWeight { get; set; } = 1.0;

    /// <summary>
    /// Default buffer window in seconds.
    /// </summary>
    public int DefaultBufferSeconds { get; set; } = 20;

    /// <summary>
    /// If true, uses pure random selection (equal chance for all).
    /// Set to true to disable all weighting.
    /// </summary>
    public bool UsePureRandom { get; set; } = false;

    /// <summary>
    /// If true, serial numbers are assigned randomly (Habbo style).
    /// If false, they are assigned sequentially (1, 2, 3...).
    /// </summary>
    public bool RandomizeSerials { get; set; } = true;

    /// <summary>
    /// If true, each player can only win one item per LTD series.
    /// If false, players can buy as many as they want (if stock permits).
    /// </summary>
    public bool LimitOnePerCustomer { get; set; } = true;

    /// <summary>
    /// Badge count weighting configuration.
    /// </summary>
    public WeightCriterion BadgeCount { get; set; } =
        new()
        {
            Enabled = true,
            BonusPerUnit = 0.02,
            MaxBonus = 1.0,
        };

    /// <summary>
    /// Account age (days) weighting configuration.
    /// </summary>
    public WeightCriterion AccountAgeDays { get; set; } =
        new()
        {
            Enabled = true,
            BonusPerUnit = 0.00137, // ~0.5 per year
            MaxBonus = 0.5,
        };

    /// <summary>
    /// Online time (minutes) weighting configuration.
    /// Note: Requires online time tracking to be implemented.
    /// </summary>
    public WeightCriterion OnlineTimeMinutes { get; set; } =
        new()
        {
            Enabled = false,
            BonusPerUnit = 0.00005,
            MaxBonus = 0.5,
        };

    /// <summary>
    /// Room count weighting configuration.
    /// </summary>
    public WeightCriterion RoomCount { get; set; } =
        new()
        {
            Enabled = false,
            BonusPerUnit = 0.05,
            MaxBonus = 0.5,
        };

    /// <summary>
    /// Furniture count weighting configuration.
    /// </summary>
    public WeightCriterion FurnitureCount { get; set; } =
        new()
        {
            Enabled = false,
            BonusPerUnit = 0.001,
            MaxBonus = 0.5,
        };

    /// <summary>
    /// Friend count weighting configuration.
    /// </summary>
    public WeightCriterion FriendCount { get; set; } =
        new()
        {
            Enabled = false,
            BonusPerUnit = 0.01,
            MaxBonus = 0.5,
        };

    /// <summary>
    /// Respects earned (from other users) weighting configuration.
    /// </summary>
    public WeightCriterion RespectsReceived { get; set; } =
        new()
        {
            Enabled = false,
            BonusPerUnit = 0.005,
            MaxBonus = 0.5,
        };

    /// <summary>
    /// Achievement score weighting configuration.
    /// </summary>
    public WeightCriterion AchievementScore { get; set; } =
        new()
        {
            Enabled = false,
            BonusPerUnit = 0.0001,
            MaxBonus = 0.5,
        };
}

/// <summary>
/// Configuration for a single weighting criterion.
/// </summary>
public class WeightCriterion
{
    /// <summary>
    /// Whether this criterion is used in weight calculation.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Bonus weight added per unit of this criterion.
    /// </summary>
    public double BonusPerUnit { get; set; }

    /// <summary>
    /// Maximum bonus that can be gained from this criterion.
    /// </summary>
    public double MaxBonus { get; set; }
}
