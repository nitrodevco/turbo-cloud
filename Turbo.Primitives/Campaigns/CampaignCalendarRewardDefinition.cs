namespace Turbo.Primitives.Campaigns;

/// <summary>
/// What a calendar door gives. The client shows the product <see cref="ProductCode"/> names (its
/// product data name) with the picture <see cref="CustomImage"/> (under <c>image.library.url</c>)
/// or else the icon of the furni <see cref="FurnitureClassName"/>: <c>CampaignCalendarDoorOpened</c>.
/// A product the client's product data lacks shows nothing: <c>HabboCampaigns.showProductNotification</c>
/// draws the door opened only for a product it knows. The rest is what is given, any mix of it.
/// </summary>
public sealed record CampaignCalendarRewardDefinition
{
    public int DayIndex { get; init; }

    /// <summary>The product data code; empty for the furni's class name.</summary>
    public string ProductCode { get; init; } = string.Empty;

    public string CustomImage { get; init; } = string.Empty;

    /// <summary>One of this furni is given, and its icon drawn when there is no custom image.</summary>
    public string FurnitureClassName { get; init; } = string.Empty;

    public string BadgeCode { get; init; } = string.Empty;

    public int Credits { get; init; }

    /// <summary>A currency type (<c>currency_types</c>) given <see cref="CurrencyAmount"/> of.</summary>
    public int? CurrencyTypeId { get; init; }

    public int CurrencyAmount { get; init; }

    /// <summary>An avatar effect given once (one timed copy).</summary>
    public int? EffectId { get; init; }

    /// <summary>What the client is told it got: the product code, else the furni's class name.</summary>
    public string ShownProductCode => ProductCode.Length > 0 ? ProductCode : FurnitureClassName;
}
