using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>
/// "Remote selection": reuses the selectors of other stacks. The selector pile of every selector
/// box it picks is run here, as its own stack runs it, and the piles' selections - furni and users
/// alike - are combined as the editor's selection
/// type says (<c>wiredfurni.params.remote_selection.type.0</c> / <c>.1</c>: union or intersection).
/// Param 1 filters the stacks (<c>remote_selection.filter</c>): 0 uses every picked one, a number
/// above 0 that many picked at random. Nested remote selectors are not followed.
/// </summary>
[RoomObjectLogic("wf_slc_remote")]
public class WiredSelectorRemoteSelection(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int TYPE_INTERSECTION = 1;

    public override int WiredCode => (int)WiredSelectorType.REMOTE_SELECTOR;

    public override bool SelectsUsers => true;

    // A negative stack count is stored as 0, all stacks (official client capture 2026-10-24,
    // WIN63-202610201032: -1 and -5 saved, re-opened as 0, 2/2; 7 with two stacks kept as 7).
    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(0, 1, 0), new WiredClampParamRule(0, int.MaxValue, 0)];

    // The selector boxes to run: the picked ones by default, or the triggering or signal furni
    // (official form contract of Remote Selector, [0,100,201], default 100).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.TriggeredItem,
                WiredFurniSourceType.SignalItems,
            ],
        ];

    public override async Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var stacks = PickedStacks(ctx);
        var pick = GetIntParamOrDefault(1, 0);

        if (pick > 0 && pick < stacks.Count)
            stacks = [.. stacks.OrderBy(_ => Random.Shared.Next()).Take(pick)];

        var intersect = GetIntParamOrDefault(0, 0) == TYPE_INTERSECTION;
        WiredSelectionSet? output = null;

        foreach (var stack in stacks)
        {
            var set = new WiredSelectionSet();

            await WiredSelectorChain.RunAsync(
                stack,
                ctx,
                set,
                FurniModule.Items,
                AvatarModule.Avatars,
                null,
                ct
            );

            if (output is null)
            {
                output = set;

                continue;
            }

            if (intersect)
            {
                output.SelectedFurniIds.IntersectWith(set.SelectedFurniIds);
                output.SelectedAvatarIds.IntersectWith(set.SelectedAvatarIds);
            }
            else
            {
                output.SelectedFurniIds.UnionWith(set.SelectedFurniIds);
                output.SelectedAvatarIds.UnionWith(set.SelectedAvatarIds);
            }
        }

        if (output is null)
            return new WiredSelectionSet();

        WiredSelectorChain.PutInRoomOrder(
            output,
            WiredSelectorChain.UserOrderOf(stacks.SelectMany(x => x))
        );

        return output;
    }

    /// <summary>
    /// The selector piles to run: each picked selector box stands for every selector on its tile,
    /// lowest first, the tile counted once (official client capture 2026-10-24, W4 lab
    /// selector.19.R7: picking one box of a parked pile with no trigger gave the furni and user
    /// selections of the whole pile, 2/2). Remote selectors are skipped, so one cannot run itself (inference: not measured).
    /// </summary>
    private List<List<IWiredSelector>> PickedStacks(IWiredProcessingContext ctx)
    {
        var stacks = new List<List<IWiredSelector>>();
        var tiles = new HashSet<int>();

        foreach (var itemId in WiredSlotSelection.ForSlot(this, ctx, 0).SelectedFurniIds)
        {
            if (
                !FurniModule.TryGetItem(itemId, out var item)
                || item is not IRoomFloorItem floorItem
                || item.Logic is not FurnitureWiredSelectorLogic picked
                || picked is WiredSelectorRemoteSelection
            )
                continue;

            var tile = MapModule.ToIdx(floorItem.X, floorItem.Y);

            if (!tiles.Add(tile))
                continue;

            stacks.Add([
                .. FurniModule
                    .GetFloorItemsOnTile(tile)
                    .OrderBy(x => x.Z.Value)
                    .Select(x => x.Logic)
                    .OfType<FurnitureWiredSelectorLogic>()
                    .Where(x => x is not WiredSelectorRemoteSelection),
            ]);
        }

        return stacks;
    }
}
