namespace Turbo.Catalog.Configuration;

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
