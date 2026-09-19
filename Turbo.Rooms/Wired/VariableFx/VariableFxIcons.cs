using System.Collections.Frozen;

namespace Turbo.Rooms.Wired.VariableFx;

/// <summary>
/// The icons a number display may carry: the ids of the client editor's icon dropdown, its
/// campaign icons included. The client resolves an icon to an asset and throws when there is
/// none, so a name that is not on this list is never sent.
/// </summary>
internal static class VariableFxIcons
{
    private static readonly FrozenSet<string> ICONS = new[]
    {
        "battery",
        "burning",
        "cash",
        "cooldown",
        "droplet",
        "energy",
        "eye",
        "fish",
        "food",
        "freezing",
        "gems",
        "gold",
        "health",
        "honor",
        "magic",
        "mana",
        "poison",
        "repairing",
        "reputation",
        "shield",
        "stamina",
        "star_power",
        "stealth",
        "timeleft",
        "upgrading",
        "wooden_logs",
        "ranch.aubergine",
        "ranch.carrot",
        "ranch.corn",
        "ranch.egg",
        "ranch.grape",
        "ranch.potato",
        "ranch.pumpkin",
        "ranch.sapling",
        "ranch.tomato",
        "ranch.wheat",
    }.ToFrozenSet();

    public static bool IsKnown(string? icon) => icon is { Length: > 0 } && ICONS.Contains(icon);
}
