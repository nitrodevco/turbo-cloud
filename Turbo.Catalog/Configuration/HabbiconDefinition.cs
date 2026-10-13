namespace Turbo.Catalog.Configuration;

/// <summary>
/// One Habbicon of a collection. With a price it is sold on its own; without one it is only
/// given (by the collection's set price, or by something else such as a reward track).
/// </summary>
public sealed class HabbiconDefinition
{
    public int Id { get; set; }

    /// <summary>The client's asset and text key (<c>habbicon_&lt;name&gt;_name</c>).</summary>
    public string Name { get; set; } = string.Empty;

    public int PriceCredits { get; set; }

    public int PriceActivityPoints { get; set; }

    /// <summary>The activity-point currency of <see cref="PriceActivityPoints"/> (5 = diamonds).</summary>
    public int ActivityPointType { get; set; }

    public bool HasPrice => PriceCredits > 0 || PriceActivityPoints > 0;
}
