using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Sets, raises or lowers the altitude of the picked furni. Params: altitude in hundredths
/// of a tile and the operator. Bounded by the room stack height.
/// </summary>
[RoomObjectLogic("wf_act_set_altitude")]
public class WiredActionSetAltitude(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.SET_FURNI_ALTITUDE;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, 8000, 0),
            new WiredEnumParamRule<WiredOperatorType>(WiredOperatorType.Set),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.TriggeredItem,
            ],
        ];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var amount = GetIntParamOrDefault(0, 0);
        var op = GetIntParamOrDefault(1, WiredOperatorType.Set);
        var maxAltitude = _roomGrain._roomConfig.MaxStackHeight.ToInt();
        var map = _roomGrain.MapModule;
        var moved = false;

        foreach (var item in GetFloorItems(ctx.GetSelection(this)))
        {
            var current = item.Z.ToInt();
            var next = op switch
            {
                WiredOperatorType.Add => current + amount,
                WiredOperatorType.Subtract => current - amount,
                _ => amount,
            };

            next = Math.Clamp(next, 0, maxAltitude);

            if (next == current)
                continue;

            moved |= await ctx.ProcessFloorItemMovementAsync(
                item,
                map.ToIdx(item.X, item.Y),
                Altitude.FromInt(next),
                item.Rotation
            );
        }

        return moved;
    }
}
