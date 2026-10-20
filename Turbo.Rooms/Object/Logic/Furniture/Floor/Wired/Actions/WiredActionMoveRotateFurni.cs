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
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

[RoomObjectLogic("wf_act_move_rotate")]
public class WiredActionMoveRotateFurni(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredMoveActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.MOVE_AND_ROTATE_FURNI;

    protected override bool AllowsWallFurni => true;

    private int _movementType = 0;
    private int _rotationType = 0;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, 11, 0), // Movement Type
            new WiredRangeParamRule(0, 3, 0), // Rotation Type
            new WiredParamRule(1),
        ];

    // The official server opens a fresh box with a third int, 1 (defaults [0,0,1],
    // 2026-10-20T17-39-35-Open-wait), that the editor neither reads nor writes (AS3 actiontypes/
    // _-M2F.as:29-37: two radio groups); a save of the two keeps it at its default.
    private const int EDITOR_PARAMS = 2;

    protected override int GetRequiredIntParamCount() => EDITOR_PARAMS;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override bool SucceedsWithoutMoving => true;

    protected override void PlanMoves(IWiredContext ctx, WiredMovePlan plan)
    {
        foreach (var furniId in ctx.GetSelection(this).SelectedFurniIds)
        {
            if (
                !FurniModule.TryGetItem(furniId, out var item)
                || item is not IRoomFloorItem floorItem
            )
                continue;

            var at = plan.Where(floorItem);
            var moveDirection = GetMoveDirection(_movementType);
            var moveRotation = GetMoveRotation(at.Rotation, _rotationType);

            // Off the edge of the room is a move that cannot be made.
            if (
                !MapModule.TryGetTileInFront(
                    MapModule.ToIdx(at.X, at.Y),
                    moveDirection,
                    out var nextIdx
                )
            )
            {
                plan.Block();

                continue;
            }

            var (targetX, targetY) = MapModule.GetTileXY(nextIdx);

            plan.Move(floorItem, targetX, targetY, null, moveRotation);
        }
    }

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _movementType = GetIntParamOrDefault(0, 0);
        _rotationType = GetIntParamOrDefault(1, 0);
    }

    public static Rotation GetMoveDirection(int movementType) =>
        movementType switch
        {
            1 => RotationExtensions.CARDINAL[Random.Shared.Next(0, 4)],
            2 => Random.Shared.NextDouble() < 0.5 ? Rotation.East : Rotation.West,
            3 => Random.Shared.NextDouble() < 0.5 ? Rotation.North : Rotation.South,
            4 => Rotation.North,
            5 => Rotation.East,
            6 => Rotation.South,
            7 => Rotation.West,
            // The editor's diagonal arrows (move_1, move_3, move_5, move_7).
            8 => Rotation.NorthEast,
            9 => Rotation.SouthEast,
            10 => Rotation.SouthWest,
            11 => Rotation.NorthWest,
            _ => Rotation.None,
        };

    public static Rotation GetMoveRotation(Rotation currentRotation, int rotationType) =>
        rotationType switch
        {
            1 => currentRotation.Rotate(+2),
            2 => currentRotation.Rotate(-2),
            3 => GetMoveRotation(currentRotation, Random.Shared.Next(2) == 0 ? 1 : 2),
            _ => currentRotation,
        };
}
