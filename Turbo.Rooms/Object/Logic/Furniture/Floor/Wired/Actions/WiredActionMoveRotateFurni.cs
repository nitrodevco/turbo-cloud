using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.IntParams;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

[RoomObjectLogic("wf_act_move_rotate")]
public class WiredActionMoveRotateFurni(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.MOVE_AND_ROTATE_FURNI;

    private int _movementType = 0;
    private int _rotationType = 0;

    public override List<WiredIntParamRule> GetIntParamRules() =>
        [
            new WiredIntRangeRule(0, 7, 0), // Movement Type
            new WiredIntRangeRule(0, 3, 0), // Rotation Type
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

    public override async Task<bool> ExecuteAsync(WiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = await ctx.GetEffectiveSelectionAsync(this, ct);
        var actionCtx = ctx.AsActionContext();

        foreach (var furniId in selection.SelectedFurniIds)
        {
            try
            {
                if (!ctx.Room._liveState.FloorItemsById.TryGetValue(furniId, out var floorItem))
                    continue;

                var moveDirection = GetMoveDirection(_movementType);
                var moveRotation = GetMoveRotation(floorItem.Rotation, _rotationType);

                if (
                    !ctx.Room._mapModule.TryGetTileInFront(
                        ctx.Room._mapModule.ToIdx(floorItem.X, floorItem.Y),
                        moveDirection,
                        out var nextIdx
                    )
                )
                    continue;

                var (targetX, targetY) = ctx.Room._mapModule.GetTileXY(nextIdx);

                if (
                    !ctx.Room._furniModule.ValidateFloorItemPlacement(
                        actionCtx,
                        furniId,
                        targetX,
                        targetY,
                        moveRotation
                    )
                )
                    continue;

                ctx.AddFloorItemMovement(floorItem, nextIdx, moveRotation);
            }
            catch
            {
                continue;
            }
        }

        return true;
    }

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        try
        {
            _movementType = WiredData.IntParams[0];
            _rotationType = WiredData.IntParams[1];
        }
        catch { }
    }

    public Rotation GetMoveDirection(int movementType) =>
        movementType switch
        {
            1 => RotationExtensions.CARDINAL[Random.Shared.Next(0, 4)],
            2 => Random.Shared.NextDouble() < 0.5 ? Rotation.East : Rotation.West,
            3 => Random.Shared.NextDouble() < 0.5 ? Rotation.North : Rotation.South,
            4 => Rotation.North,
            5 => Rotation.East,
            6 => Rotation.South,
            7 => Rotation.West,
            _ => Rotation.None,
        };

    public Rotation GetMoveRotation(Rotation currentRotation, int rotationType) =>
        rotationType switch
        {
            1 => currentRotation.Rotate(+2),
            2 => currentRotation.Rotate(-2),
            3 => GetMoveRotation(currentRotation, Random.Shared.Next(2) == 0 ? 1 : 2),
            _ => currentRotation,
        };
}
