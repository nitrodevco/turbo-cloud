using System.Globalization;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// A group furni's stuff: five strings, in the order the client reads them. The item stores its
/// state and its group; the room fills in the group's badge and colours as the item loads, so a
/// stored item leaves them empty. The catalog, the room's logic and the Morningstar import all
/// write this layout.
/// </summary>
public static class GuildFurnitureData
{
    public const int STATE_SLOT = 0;
    public const int GUILD_ID_SLOT = 1;
    public const int BADGE_CODE_SLOT = 2;
    public const int PRIMARY_COLOR_SLOT = 3;
    public const int SECONDARY_COLOR_SLOT = 4;
    public const int SLOT_COUNT = 5;

    /// <summary>The stuff of a group furni in a state, for a group, its look left to the room.</summary>
    public static string[] Compose(string state, int guildId) =>
        [state, guildId.ToString(CultureInfo.InvariantCulture), "", "", ""];
}
