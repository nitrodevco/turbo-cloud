using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Wired;

/// <summary>
/// One action's furni moves, worked out on a <see cref="WiredGhostLayout"/> before any of them is
/// made: every random roll is taken here, once, so the moves made are the moves planned. Applying
/// it makes them in order, each checked again against the room as it then is; a move the room
/// no longer takes is skipped. What the
/// action does besides moving (a collision, a state, a heading it remembers) is held back with
/// <see cref="Then(Action)"/> and happens in its place among the moves.
/// </summary>
public sealed class WiredMovePlan
{
    private readonly WiredGhostLayout _layout;
    private readonly IWiredPolicy _policy;
    private readonly RoomMapModule _map;
    private readonly List<Func<IWiredExecutionContext, Task<bool>>> _steps = [];

    internal WiredMovePlan(WiredGhostLayout layout, IWiredPolicy policy, RoomMapModule map)
    {
        _layout = layout;
        _policy = policy;
        _map = map;
    }

    /// <summary>
    /// Whether the action meant to move a furni and could not: what "Can Perform Movements"
    /// fails on.
    /// </summary>
    public bool HasBlockedMove { get; private set; }

    /// <summary>Where a furni stands once the moves planned before this one are made.</summary>
    public WiredFurniPlacement Where(IRoomFloorItem item) => _layout.Where(item);

    /// <summary>A move the action makes once: refused, it is a blocked move.</summary>
    public bool Move(
        IRoomFloorItem item,
        int x,
        int y,
        Altitude? z = null,
        Rotation? rotation = null
    )
    {
        if (TryMove(item, x, y, z, rotation))
            return true;

        Block();

        return false;
    }

    /// <summary>
    /// One attempt of a move the action may try another way (another heading, another tile):
    /// refused, it counts for nothing, and the action calls <see cref="Block"/> once it gives up.
    /// </summary>
    public bool TryMove(
        IRoomFloorItem item,
        int x,
        int y,
        Altitude? z = null,
        Rotation? rotation = null
    )
    {
        var rot = rotation ?? Where(item).Rotation;

        if (!_layout.CanMove(item, x, y, rot, _policy))
            return false;

        var from = Where(item);

        if (_policy.MovePhysics.HasFlag(WiredMovePhysicsFlags.KeepAltitude))
            _layout.Move(item, x, y, z ?? from.Z, rot);
        else
            _layout.Move(item, x, y, z, rot);

        // A turn in place lands where the layout put it (WiredGhostLayout.Move), which the map
        // measured from the anchor tile alone would not.
        var landing =
            z ?? (from.X == x && from.Y == y && from.Rotation != rot ? Where(item).Z : null);

        _steps.Add(ctx => ctx.TryMoveFloorItemAsync(item, x, y, landing, rot));

        return true;
    }

    /// <summary>
    /// Changes only a furni's altitude, on whatever tile it stands on when the move is made; no
    /// placement is asked, the altitude being the action's to set. The users standing on it go up
    /// or down with it, carry add-on or not (official client capture 2026-10-24, W1 lab
    /// action.29.R2: in the furni's WiredMovements, 1/1 with the add-on, 3/3 without).
    /// </summary>
    public void SetAltitude(IRoomFloorItem item, Altitude z)
    {
        var at = Where(item);

        _layout.Move(item, at.X, at.Y, z, at.Rotation);
        _steps.Add(ctx =>
            ctx.ProcessFloorItemMovementAsync(
                item,
                _map.ToIdx(item.X, item.Y),
                z,
                item.Rotation,
                carryStanding: true
            )
        );
    }

    /// <summary>
    /// A wall item's move, sent in the action's WiredMovements as a wall move, also when it
    /// stays where it is (<see cref="WiredWallMoves"/>). Nothing on the floor is in its way and
    /// it is in nobody's.
    /// </summary>
    public void MoveWallItem(IRoomWallItem item, WallPosition to) =>
        _steps.Add(async ctx =>
        {
            await ctx.ProcessWallItemMovementAsync(
                item,
                to.X,
                to.Y,
                to.Z,
                to.Rotation,
                to.WallOffset
            );

            return true;
        });

    /// <summary>A wall item turned onto the other wall side, sent as its own item update.</summary>
    public void TurnWallItem(IRoomWallItem item, WallPosition to) =>
        _steps.Add(async ctx =>
        {
            if (ctx is WiredExecutionContext execution)
                await execution.TurnWallItemAsync(item, to);

            return true;
        });

    /// <summary>The action meant to move a furni and found no way to.</summary>
    public void Block() => HasBlockedMove = true;

    /// <summary>Something the action does besides moving, in its place among the moves.</summary>
    public void Then(Action effect) =>
        _steps.Add(_ =>
        {
            effect();

            return Task.FromResult(false);
        });

    /// <summary>Something the action does besides moving that has to be awaited.</summary>
    public void Then(Func<IWiredExecutionContext, Task> effect) =>
        _steps.Add(async ctx =>
        {
            await effect(ctx);

            return false;
        });

    /// <summary>Makes the planned moves; true when at least one furni moved.</summary>
    internal async Task<bool> ApplyAsync(IWiredExecutionContext ctx)
    {
        var moved = false;

        foreach (var step in _steps)
            moved |= await step(ctx);

        return moved;
    }
}
