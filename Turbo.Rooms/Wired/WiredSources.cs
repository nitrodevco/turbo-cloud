using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>
/// The source sets most boxes offer in the editor. The first entry of a set is the box's
/// default, so the order is part of the meaning. A box with its own set (a different default,
/// fewer choices, bots by name) still spells it out.
/// </summary>
internal static class WiredSources
{
    /// <summary>The users a stack can act on: whoever triggered it, a selector's pick, or a signal's.</summary>
    public static WiredPlayerSourceType[] Users =>
        [
            WiredPlayerSourceType.TriggeredUser,
            WiredPlayerSourceType.SelectorUsers,
            WiredPlayerSourceType.SignalUsers,
        ];

    /// <summary>The bot a bot box names in its text; bots are not in selections.</summary>
    public static WiredPlayerSourceType[] BotByName => [WiredPlayerSourceType.BotByName];

    /// <summary>The furni a stack can act on, defaulting to the furni picked in the box.</summary>
    public static WiredFurniSourceType[] Furni =>
        [
            WiredFurniSourceType.SelectedItems,
            WiredFurniSourceType.SelectorItems,
            WiredFurniSourceType.SignalItems,
            WiredFurniSourceType.TriggeredItem,
        ];

    /// <summary>Only furni the box or a selector names, for boxes a triggering furni makes no sense for.</summary>
    public static WiredFurniSourceType[] PickedFurni =>
        [WiredFurniSourceType.SelectedItems, WiredFurniSourceType.SelectorItems];
}
