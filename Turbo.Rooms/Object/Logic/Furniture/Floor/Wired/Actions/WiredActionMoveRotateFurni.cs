using System;
using System.Collections.Generic;
using System.Linq;
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
            new WiredParamRule(0),
        ];

    // The official server opens a fresh box with a third int, 1 (defaults [0,0,1],
    // 2026-10-20T17-39-35-Open-wait), that the editor neither reads nor writes (AS3 actiontypes/
    // _-M2F.as:29-37: two radio groups); a save of the two stores it as 0 (official client
    // capture 2026-10-24, W1 lab action.4.E1, 2/2). Neither value changed a move (action.4.B1).
    private const int EDITOR_PARAMS = 2;

    public override List<int> GetDefaultIntParams() => [0, 0, 1];

    protected override int GetRequiredIntParamCount() => EDITOR_PARAMS;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override bool SucceedsWithoutMoving => true;

    /// <summary>
    /// The picks move from the top down, each onto the room as the ones before left it: a block
    /// and the trophy on it, both picked and moved one tile, move the trophy alone, which drops
    /// to the floor and leaves the block blocked by it, on every later firing too; the block
    /// picked alone moves without the trophy, which stays where it was (official client capture
    /// 2026-10-24T18-40-50, W1 lab action.4.X9 2/2, action.4.X9b 2/2). Inference: the order is
    /// highest altitude first, then lowest id - the one order that gives both firings of X9.
    /// </summary>
    protected override void PlanMoves(IWiredContext ctx, WiredMovePlan plan)
    {
        var picks = ctx.GetSelection(this)
            .SelectedFurniIds.Select(id =>
                FurniModule.TryGetItem(id, out var item) ? item as IRoomFloorItem : null
            )
            .OfType<IRoomFloorItem>()
            .OrderByDescending(item => plan.Where(item).Z.Value)
            .ThenBy(item => item.ObjectId.Value)
            .ToList();

        foreach (var floorItem in picks)
        {
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

            // A user in the way blocks it, with no collision (official client capture 2026-10-24,
            // W1 lab action.4.B1, 4/4).
            if (IsBlockedByUser(ctx, floorItem, at, targetX, targetY, moveRotation))
            {
                plan.Block();

                continue;
            }

            plan.Move(floorItem, targetX, targetY, null, moveRotation);
        }

        // A wall pick moves along its wall, a rotation turning it onto the other side (W1 lab
        // action.4.B11: [4,1,1], 2/2; action.4.X11: [4,0,0] and [5,0,0], 2/2 each).
        var (dx, dy) = MapModule.GetDirectionOffset(GetMoveDirection(_movementType));
        var turn = _rotationType != 0;

        if (dx != 0 || dy != 0 || turn)
            PlanWallItemSteps(ctx.GetSelection(this), plan, dx, dy, turn);
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
            // Any of the eight ways, diagonals included (official client capture
            // 2026-10-24T08-13-33, W1 lab action.4.R9); a blocked draw is no move, no redraw.
            1 => (Rotation)Random.Shared.Next(0, 8),
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
