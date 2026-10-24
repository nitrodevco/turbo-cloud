using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>
/// What a pile of selectors picks together, as a stack's selectors do and as Remote Selector runs
/// the pile of each box it picks. The non-filter selectors' picks add up first; the "filter
/// existing selection" ones then narrow them, wherever they stand in the pile (official client
/// capture 2026-10-24, W4 lab selector.7.R3: a filter below or above its seed gave the same
/// result 1/1 each). The result comes out in one room order, whatever the pick order or the
/// selector that picked an entry (selector.1.R4: picks [chair, lamp, table] and [lamp, table,
/// chair] listed alike 2/2; selector.19.R4: a union interleaved, not one selector after the
/// other). Habbo's own room order is not the ids' (selector.0.R4); ours is, an inference that
/// keeps the one measured property: the order does not depend on how the furni were picked.
/// Users come in the order of the selector that picked them (<see cref="IWiredSelector.UserOrder"/>):
/// Users By Type, By Name (whatever its list order) and In Area put the user who entered or
/// re-entered last in front (official client capture 2026-10-24, two accounts, W4 lab
/// selector.2/11/13.R4 and R4b, 2/2 each); Users Performing Action and In Neighborhood list by
/// ascending room index, bots included (selector.9/12.R4, 2/2 each). Where a newest-first
/// selector puts a bot is open: ours sorts it by its room index, but Users By Name picking two
/// bots listed the one placed first first (2026-10-24T17-24-24) and In Area put a bot placed last
/// between two users (selector.13.R4). A pile mixing both orders following its first user
/// selector that is not a filter is inferred.
/// </summary>
public static class WiredSelectorChain
{
    private static readonly bool[] FILTER_PASSES = [false, true];

    public static async Task RunAsync(
        IReadOnlyList<IWiredSelector> selectors,
        IWiredProcessingContext ctx,
        IWiredSelectionSet pool,
        IReadOnlyCollection<IRoomItem> roomItems,
        IReadOnlyCollection<IRoomAvatar> roomAvatars,
        Action<IWiredSelector>? ran,
        CancellationToken ct
    )
    {
        foreach (var filters in FILTER_PASSES)
        {
            foreach (var selector in selectors)
            {
                if (selector.GetIsFilter() != filters)
                    continue;

                await ApplyAsync(selector, ctx, pool, roomItems, roomAvatars, ct);

                ran?.Invoke(selector);
            }
        }

        PutInRoomOrder(pool, UserOrderOf(selectors));
    }

    /// <summary>
    /// The order a pile lists its users in: that of its first user selector that picks rather
    /// than filters (filters only narrow what it picked).
    /// </summary>
    public static WiredUserOrderType UserOrderOf(IEnumerable<IWiredSelector> selectors) =>
        selectors.FirstOrDefault(x => x.SelectsUsers && !x.GetIsFilter())?.UserOrder
        ?? WiredUserOrderType.NewestFirst;

    /// <summary>
    /// Adds a selector's result to the pool. Filter selectors narrow what the other selectors
    /// picked and pick nothing on their own: with no seed the pool stays empty, the triggering
    /// user or a signal's furni included (official client capture: a users-by-type filter alone
    /// selected no user, and a furni-with-variable filter whose "furni from signal" seed was moved
    /// off the stack forwarded nothing). Filtering and inverting work on the selector's own kind,
    /// furni or users, and leave the other kind in the pool as it was.
    /// </summary>
    private static async Task ApplyAsync(
        IWiredSelector selector,
        IWiredProcessingContext ctx,
        IWiredSelectionSet pool,
        IReadOnlyCollection<IRoomItem> roomItems,
        IReadOnlyCollection<IRoomAvatar> roomAvatars,
        CancellationToken ct
    )
    {
        var set = await selector.SelectAsync(ctx, ct);

        if (selector.GetIsInvert())
            set = Invert(selector, set, roomItems, roomAvatars);

        if (!selector.GetIsFilter())
        {
            pool.UnionWith(set);

            return;
        }

        if (selector.SelectsFurni)
            pool.SelectedFurniIds.IntersectWith(set.SelectedFurniIds);

        if (selector.SelectsUsers)
            pool.SelectedAvatarIds.IntersectWith(set.SelectedAvatarIds);
    }

    /// <summary>
    /// What an inverted selector picks: every furni or user of its kind in the room that it did
    /// not match (<c>wiredfurni.danger.1.change.confirm.body</c>: "all furni/users that do NOT
    /// match the requirement"), wired boxes and the selector itself included (official client
    /// capture 2026-10-24, W4 lab selector.0.R3 / selector.5.R3).
    /// </summary>
    private static WiredSelectionSet Invert(
        IWiredSelector selector,
        IWiredSelectionSet set,
        IReadOnlyCollection<IRoomItem> roomItems,
        IReadOnlyCollection<IRoomAvatar> roomAvatars
    )
    {
        var inverted = new WiredSelectionSet();

        if (selector.SelectsFurni)
        {
            foreach (var item in roomItems)
            {
                if (!set.SelectedFurniIds.Contains(item.ObjectId))
                    inverted.SelectedFurniIds.Add(item.ObjectId);
            }
        }

        if (selector.SelectsUsers)
        {
            foreach (var avatar in roomAvatars)
            {
                if (!set.SelectedAvatarIds.Contains(avatar.ObjectId))
                    inverted.SelectedAvatarIds.Add(avatar.ObjectId);
            }
        }

        return inverted;
    }

    /// <summary>
    /// Rebuilds the pool in room order: furni by id, users newest first (a room index is handed out
    /// on every entry, so the highest is the user who came in last) or by ascending room index.
    /// </summary>
    public static void PutInRoomOrder(IWiredSelectionSet pool, WiredUserOrderType userOrder)
    {
        if (pool.SelectedFurniIds.Count > 1)
        {
            var furni = pool.SelectedFurniIds.Order().ToArray();

            pool.SelectedFurniIds.Clear();
            pool.SelectedFurniIds.UnionWith(furni);
        }

        if (pool.SelectedAvatarIds.Count > 1)
        {
            var avatars =
                userOrder == WiredUserOrderType.RoomIndex
                    ? pool.SelectedAvatarIds.OrderBy(x => x.Value).ToArray()
                    : pool.SelectedAvatarIds.OrderByDescending(x => x.Value).ToArray();

            pool.SelectedAvatarIds.Clear();
            pool.SelectedAvatarIds.UnionWith(avatars);
        }
    }
}
