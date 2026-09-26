using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Puts the picked furni back the way they were when the box was saved. The four params pick
/// what to restore: state, direction, position, altitude.
/// </summary>
[RoomObjectLogic("wf_act_match_to_sshot")]
public class WiredActionMatchToSnapshot(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.SET_FURNI_STATE;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false), // state
            new WiredBoolParamRule(false), // direction
            new WiredBoolParamRule(false), // position
            new WiredBoolParamRule(false), // altitude
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [WiredFurniSourceType.SelectedItems],
        ];

    protected override bool KeepsFurniSnapshot => true;

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var snapshot = GetFurniSnapshot();
        var restoreState = GetIntParamOrDefault(0, false);
        var restoreRotation = GetIntParamOrDefault(1, false);
        var restorePosition = GetIntParamOrDefault(2, false);
        var restoreAltitude = GetIntParamOrDefault(3, false);
        var actionCtx = ctx.AsActionContext();

        foreach (var (itemId, entry) in snapshot)
        {
            if (!TryGetFloorItem(itemId, out var item))
                continue;

            if (restoreState && item.Logic.GetState() != entry.State)
                await ctx.ProcessItemStateUpdateAsync(item, entry.State);

            var targetX = restorePosition ? entry.X : item.X;
            var targetY = restorePosition ? entry.Y : item.Y;
            var targetRotation = restoreRotation ? (Rotation)entry.Rotation : item.Rotation;
            Altitude? targetZ = restoreAltitude ? Altitude.FromInt(entry.Z) : null;

            if (
                targetX == item.X
                && targetY == item.Y
                && targetRotation == item.Rotation
                && (targetZ is null || targetZ == item.Z)
            )
                continue;

            if (
                !_roomGrain.MapModule.InBounds(targetX, targetY)
                || !await _roomGrain.FurniModule.ValidateFloorItemPlacementAsync(
                    actionCtx,
                    itemId,
                    targetX,
                    targetY,
                    targetRotation
                )
            )
                continue;

            await ctx.ProcessFloorItemMovementAsync(
                item,
                _roomGrain.MapModule.ToIdx(targetX, targetY),
                targetZ,
                targetRotation
            );
        }

        return true;
    }
}
