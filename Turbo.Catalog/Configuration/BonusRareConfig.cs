namespace Turbo.Catalog.Configuration;

/// <summary>The reception's purchased-credit reward campaign.</summary>
public sealed class BonusRareConfig
{
    public bool Enabled { get; init; } = true;

    /// <summary>Change this when starting a new campaign so progress is not carried over.</summary>
    public string CampaignId { get; init; } = "bonusbag26_3";

    /// <summary>Furniture definition name; the wire class id is resolved from its SpriteId.</summary>
    public string FurnitureName { get; init; } = "bonusbag26_3";

    /// <summary>Product-data key used by BonusRarePromoWidget to name the reward.</summary>
    public string ProductCode { get; init; } = "bonusbag26_3";

    /// <summary>Eligible purchased credits per reward, independent of the current wallet balance.</summary>
    public int CreditsRequired { get; init; } = 120;
}
