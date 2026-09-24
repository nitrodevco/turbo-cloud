using System;

namespace Turbo.Primitives.Guilds;

/// <summary>
/// The furniture logic names that make a piece of furni a group's. A definition carrying one of
/// these is bought for a group rather than for a player: the purchase carries the group id and
/// the item wears that group's badge and colours.
///
/// They are here rather than in the room module because the catalog has to recognise one before
/// the item exists.
/// </summary>
public static class GuildFurnitureLogicNames
{
    public const string CUSTOMIZED = "guild_customized";
    public const string FORUM = "guild_forum";

    public static bool IsGuildFurniture(string? logicName) =>
        string.Equals(logicName, CUSTOMIZED, StringComparison.Ordinal)
        || string.Equals(logicName, FORUM, StringComparison.Ordinal);
}
