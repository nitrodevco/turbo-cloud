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
/// "Remote selection": reuses the selectors of other stacks. Every selector box it picks is run
/// here, and their selections - furni and users alike - are combined as the editor's selection
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

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(0, 1, 0), WiredRules.AnyInt()];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [WiredFurniSourceType.SelectedItems],
        ];

    public override async Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var remotes = new List<FurnitureWiredSelectorLogic>();

        foreach (var itemId in GetStuffIds())
        {
            if (
                FurniModule.TryGetItem(itemId, out var item)
                && item.Logic is FurnitureWiredSelectorLogic remote
                && remote is not WiredSelectorRemoteSelection
            )
                remotes.Add(remote);
        }

        var pick = GetIntParamOrDefault(1, 0);

        if (pick > 0 && pick < remotes.Count)
            remotes = [.. remotes.OrderBy(_ => Random.Shared.Next()).Take(pick)];

        var intersect = GetIntParamOrDefault(0, 0) == TYPE_INTERSECTION;
        WiredSelectionSet? output = null;

        foreach (var remote in remotes)
        {
            var set = await remote.SelectAsync(ctx, ct);

            if (output is null)
            {
                output = new WiredSelectionSet();
                output.SelectedFurniIds.UnionWith(set.SelectedFurniIds);
                output.SelectedAvatarIds.UnionWith(set.SelectedAvatarIds);

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

        return output ?? new WiredSelectionSet();
    }
}
