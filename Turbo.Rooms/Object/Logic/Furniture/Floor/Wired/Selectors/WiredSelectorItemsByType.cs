using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>
/// Picks every furni of the kinds its source furni are. Param 0 is the editor's "state match"
/// checkbox: with it on a furni only counts when it is in the same state as a source furni of its
/// kind, so a stack of gates picks the open ones when an open one is the source.
/// </summary>
[RoomObjectLogic("wf_slc_furni_bytype")]
public class WiredSelectorItemsByType(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.FURNI_BY_TYPE;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.TriggeredItem,
            ],
        ];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var input = ctx.GetSelection(this);
        var matchState = GetIntParamOrDefault(0, false);
        var kinds = new HashSet<(int Definition, int State)>();
        var output = new WiredSelectionSet();

        foreach (var id in input.SelectedFurniIds)
        {
            if (FurniModule.TryGetItem(id, out var item))
                kinds.Add(KindOf(item, matchState));
        }

        foreach (var item in FurniModule.Items)
        {
            if (kinds.Contains(KindOf(item, matchState)))
                output.SelectedFurniIds.Add((int)item.ObjectId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }

    /// <summary>
    /// What makes two furni "the same": their definition, and with the state option on, the state
    /// they are in as well (an open and a closed gate are then two kinds).
    /// </summary>
    private static (int Definition, int State) KindOf(IRoomItem item, bool matchState) =>
        (item.Definition.Id, matchState ? item.Logic.GetState() : 0);
}
