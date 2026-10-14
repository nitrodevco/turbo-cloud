using System;
using System.Linq;
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

    /// <summary>
    /// The furni of a box's second slot: the box's second pick list (the editor's "secondary
    /// picks", which makes it offer two pick lists), then the same pool sources as <see cref="Furni"/>.
    /// </summary>
    public static WiredFurniSourceType[] SecondaryFurni =>
        [
            WiredFurniSourceType.SecondaryItems,
            WiredFurniSourceType.SelectorItems,
            WiredFurniSourceType.SignalItems,
            WiredFurniSourceType.TriggeredItem,
        ];

    /// <summary>Only furni the box or a selector names, for boxes a triggering furni makes no sense for.</summary>
    public static WiredFurniSourceType[] PickedFurni =>
        [WiredFurniSourceType.SelectedItems, WiredFurniSourceType.SelectorItems];

    /// <summary>
    /// Every slot of a box that takes two pick lists: both pick lists and the pool sources. The
    /// official editor offers all five on each slot of such a box (official client capture:
    /// send signal, initiate transaction, chest contains furni type, the chest scanner, ...).
    /// </summary>
    public static WiredFurniSourceType[] FurniOrSecondary =>
        [
            WiredFurniSourceType.SelectedItems,
            WiredFurniSourceType.SecondaryItems,
            WiredFurniSourceType.SelectorItems,
            WiredFurniSourceType.SignalItems,
            WiredFurniSourceType.TriggeredItem,
        ];

    /// <summary>
    /// The same set with <paramref name="first"/> moved to the front, which makes it the slot's
    /// default.
    /// </summary>
    public static T[] DefaultingTo<T>(this T[] sources, T first)
        where T : struct, Enum => [first, .. sources.Where(x => !x.Equals(first))];
}
