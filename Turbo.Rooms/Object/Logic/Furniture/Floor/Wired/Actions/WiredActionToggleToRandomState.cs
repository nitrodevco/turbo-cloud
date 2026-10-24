using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>Sets the picked furni to a random one of their states, their current one included.</summary>
[RoomObjectLogic("wf_act_toggle_to_rnd")]
public class WiredActionToggleToRandomState(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.TOGGLE_TO_RANDOM_STATE;

    protected override bool AllowsWallFurni => true;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = ctx.GetSelection(this);
        var changed = false;

        foreach (var itemId in selection.SelectedFurniIds)
        {
            if (!FurniModule.TryGetItem(itemId, out var item))
                continue;

            var totalStates = Math.Max(1, item.Definition.TotalStates);
            var state = Random.Shared.Next(totalStates);

            // The draw may land on the state the furni is already in: Habbo then sends no state
            // update and "Furni state changes" does not fire (official client capture, 5 of 12
            // draws on a 2-state lamp).
            if (state != item.Logic.GetState())
                await ctx.ProcessItemStateUpdateAsync(item, state);

            changed = true;
        }

        return changed;
    }
}
