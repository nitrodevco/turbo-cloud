using Turbo.Players.Configuration;
using Turbo.Primitives.Badges.Enums;

namespace Turbo.Players.Badges;

/// <summary>Owner count to rarity tier, by the limits in <see cref="BadgeConfig"/>.</summary>
internal static class BadgeRarityCalculator
{
    public static BadgeRarityType Calculate(BadgeConfig config, int ownerCount, int totalPlayers)
    {
        if (ownerCount <= 0 || totalPlayers < config.MinimumPlayersForRarity)
            return BadgeRarityType.Common;

        if (ownerCount <= config.UniqueMaxOwners)
            return BadgeRarityType.Unique;

        var share = (double)ownerCount / totalPlayers;

        if (share <= config.LegendaryMaxOwnerShare)
            return BadgeRarityType.Legendary;

        if (share <= config.MythicalMaxOwnerShare)
            return BadgeRarityType.Mythical;

        if (share <= config.EpicMaxOwnerShare)
            return BadgeRarityType.Epic;

        if (share <= config.RareMaxOwnerShare)
            return BadgeRarityType.Rare;

        return share <= config.UncommonMaxOwnerShare
            ? BadgeRarityType.Uncommon
            : BadgeRarityType.Common;
    }
}
