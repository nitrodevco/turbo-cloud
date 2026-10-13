using System.Collections.Generic;

namespace Turbo.Catalog.Configuration;

/// <summary>
/// A Habbicon collection: its Habbicons, the reward for owning them all, and the price of the
/// whole set (none: the set is not sold as one).
/// </summary>
public sealed class HabbiconCollectionDefinition
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>The Habbicon a player can claim once they own every one of the collection; 0 for none.</summary>
    public int RewardHabbiconId { get; set; }

    /// <summary>The reward's asset and text key, for <c>HabbiconInfo</c>; empty when unknown.</summary>
    public string RewardHabbiconName { get; set; } = string.Empty;

    public int PriceCredits { get; set; }

    public int PriceActivityPoints { get; set; }

    public int ActivityPointType { get; set; }

    public List<HabbiconDefinition> Habbicons { get; set; } = [];

    public bool HasPrice => PriceCredits > 0 || PriceActivityPoints > 0;
}
