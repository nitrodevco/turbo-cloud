using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>
/// Resolves one input slot of a box on its own. Boxes with two furni slots (move furni to,
/// furni to furni, send signal) keep the slots apart: slot 0 comes from the first source
/// list and the first pick list, slot 1 from the second.
/// </summary>
public static class WiredSlotSelection
{
    public static IWiredSelectionSet ForSlot(IWiredBox box, IWiredContext ctx, int slot)
    {
        var set = new WiredSelectionSet();
        var furniSources = box.GetFurniSources();

        if (slot >= furniSources.Count)
            return set;

        foreach (var sourceType in furniSources[slot])
        {
            switch (sourceType)
            {
                case WiredFurniSourceType.TriggeredItem:
                    set.SelectedFurniIds.UnionWith(ctx.Selected.SelectedFurniIds);
                    break;
                case WiredFurniSourceType.SelectedItems:
                case WiredFurniSourceType.SnapshotItems:
                    set.SelectedFurniIds.UnionWith(
                        slot == 0 ? box.GetStuffIds() : box.GetStuffIds2()
                    );
                    break;
                case WiredFurniSourceType.SecondaryItems:
                    set.SelectedFurniIds.UnionWith(box.GetStuffIds2());
                    break;
                case WiredFurniSourceType.SelectorItems:
                    set.SelectedFurniIds.UnionWith(ctx.SelectorPool.SelectedFurniIds);
                    break;
                case WiredFurniSourceType.SignalItems:
                    set.SelectedFurniIds.UnionWith(ctx.Signal.SelectedFurniIds);
                    break;
                case WiredFurniSourceType.AllRoomItems:
                    set.UnionWith(ctx.GetSelection(box));
                    break;
            }
        }

        return set;
    }

    /// <summary>
    /// The users of one user input slot on its own, for boxes whose second user slot means
    /// something else than the first (a variable reference beside the users to reward).
    /// </summary>
    public static IWiredSelectionSet ForUserSlot(IWiredBox box, IWiredContext ctx, int slot)
    {
        var set = new WiredSelectionSet();
        var playerSources = box.GetPlayerSources();

        if (slot >= playerSources.Count)
            return set;

        foreach (var sourceType in playerSources[slot])
        {
            switch (sourceType)
            {
                case WiredPlayerSourceType.TriggeredUser:
                case WiredPlayerSourceType.ReachedUser:
                case WiredPlayerSourceType.ClickedUser:
                    set.SelectedAvatarIds.UnionWith(ctx.Selected.SelectedAvatarIds);
                    break;
                case WiredPlayerSourceType.SelectorUsers:
                    set.SelectedAvatarIds.UnionWith(ctx.SelectorPool.SelectedAvatarIds);
                    break;
                case WiredPlayerSourceType.SignalUsers:
                    set.SelectedAvatarIds.UnionWith(ctx.Signal.SelectedAvatarIds);
                    break;
            }
        }

        return set;
    }
}
