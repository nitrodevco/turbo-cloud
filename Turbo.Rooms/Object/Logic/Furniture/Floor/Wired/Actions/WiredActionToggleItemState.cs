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
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

[RoomObjectLogic("wf_act_toggle_state")]
public class WiredActionToggleItemState(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.TOGGLE_FURNI_STATE;

    protected override bool AllowsWallFurni => true;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false), // Toggle type
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = ctx.GetSelection(this);

        foreach (var furniId in selection.SelectedFurniIds)
        {
            try
            {
                // A signal antenna keeps its state: Habbo sent no update for one the toggle was
                // given (official client capture 2026-10-24T08-37-13, 2/2); its flash is the
                // signal's (RoomWiredSystem.FlashAntennas).
                if (
                    !FurniModule.TryGetItem(furniId, out var item)
                    || !item.Logic.CanWiredToggle()
                    || IsListedAntenna(item)
                )
                    continue;

                var state = _wiredData.GetIntParam<bool>(0) switch
                {
                    false => item.Logic.GetNextToggleableState(),
                    true => item.Logic.GetPrevToggleableState(),
                };

                // A furni without states has nothing to toggle to: it is skipped
                // (WiredExecutionContext.ProcessItemStateUpdateAsync).
                await ctx.ProcessItemStateUpdateAsync(item, state);
            }
            catch (Exception ex)
            {
                LogWiredDataFault(ex);

                continue;
            }
        }

        return true;
    }
}
