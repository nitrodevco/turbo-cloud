using System.Collections.Generic;
using System.Linq;

namespace Turbo.Catalog.Configuration;

/// <summary>
/// The hotel's Habbicons: collections of small icons a player owns and shows over their avatar
/// or sends in the messenger. With none listed, Habbo's collections are offered
/// (<see cref="HabboCollections"/>, <see cref="HabboHabbicons"/>).
/// </summary>
public sealed class HabbiconConfig
{
    public const string SECTION_NAME = "Turbo:Habbicons";

    /// <summary>When off, nothing is sent at login and every Habbicon request is ignored.</summary>
    public bool Enabled { get; set; } = true;

    public List<HabbiconCollectionDefinition> Collections { get; set; } = [];

    /// <summary>
    /// Offers Habbo's collections when <see cref="Collections"/> lists none. A hotel that lists
    /// collections of its own offers only those.
    /// </summary>
    public bool HabboCollections { get; set; } = true;

    /// <summary>
    /// How many recently used Habbicons are kept and sent at login; the client keeps ten
    /// (<c>HabbiconController.addRecentHabbiconId</c>).
    /// </summary>
    public int MaxRecentHabbicons { get; set; } = 10;

    /// <summary>What is wrong with the collections, or null when they can be used.</summary>
    public string? Problem()
    {
        var habbiconIds = Collections
            .SelectMany(x => x.Habbicons.Select(h => h.Id))
            .Concat(Collections.Where(x => x.RewardHabbiconId > 0).Select(x => x.RewardHabbiconId))
            .ToList();

        if (Collections.Select(x => x.Id).Distinct().Count() != Collections.Count)
            return "collection ids must be unique";

        if (habbiconIds.Any(x => x <= 0) || habbiconIds.Distinct().Count() != habbiconIds.Count)
            return "Habbicon ids (rewards included) must be positive and unique";

        if (
            Collections.Any(x =>
                x.PriceCredits < 0
                || x.PriceActivityPoints < 0
                || x.Habbicons.Any(h => h.PriceCredits < 0 || h.PriceActivityPoints < 0)
            )
        )
            return "prices must not be negative";

        return MaxRecentHabbicons < 0 ? "MaxRecentHabbicons must not be negative" : null;
    }
}
