using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Wired;

/// <summary>
/// The room as it will be once the furni moves planned so far are made, so a stack's moves can be
/// checked before any of them happens. A furni moved twice starts its second move where the first
/// left it. Tiles no planned move touched are read straight from the room, and the tiles a move
/// did touch are stacked by the map's own rules (<see cref="RoomMapModule.EvaluateTile"/>).
/// Avatars are not moved here: a user the "carry users" addon would drag along still counts
/// where it stands. With nothing planned it is the room as it stands, which is how a move made
/// at once is checked.
/// </summary>
internal sealed class WiredGhostLayout(RoomMapModule map, RoomFurniModule furni) : IRoomTileView
{
    private readonly Dictionary<RoomObjectId, WiredFurniPlacement> _placements = [];
    private readonly Dictionary<int, List<IRoomFloorItem>> _stacks = [];

    public WiredFurniPlacement Where(IRoomFloorItem item) =>
        _placements.TryGetValue(item.ObjectId, out var placement)
            ? placement
            : WiredFurniPlacement.Of(item);

    public RoomTileState GetTile(int tileIdx) =>
        _stacks.TryGetValue(tileIdx, out var stack)
            ? map.EvaluateTile(tileIdx, stack, item => Where(item).Z)
            : map.GetTile(tileIdx);

    /// <summary>
    /// Whether the stack's move physics let a furni move to a tile and rotation here: "blocked by
    /// furni" first, then the placement every mover asks, with "move through furni" letting it
    /// onto what it names.
    /// </summary>
    public bool CanMove(
        IRoomFloorItem item,
        int x,
        int y,
        Rotation rotation,
        IWiredPolicy policy
    ) =>
        !IsBlockedByFurni(item, x, y, rotation, policy)
        && furni.CanPlaceFloorItem(
            item,
            x,
            y,
            rotation,
            policy.MovePhysics.HasFlag(WiredMovePhysicsFlags.MoveThroughFurni)
                ? top => policy.MoveThroughFurniIds?.Contains(top.ObjectId.Value) ?? true
                : null,
            this
        );

    /// <summary>
    /// Puts a furni where the move would: on the top of the target tile, measured without the
    /// furni itself, unless an altitude is given (<see cref="RoomMapModule.MoveFloorItem"/>).
    /// A furni turned in place rises onto the highest tile its new footprint covers and never
    /// sinks: a 2x1 sofa turned onto a stackable block rose to the block's top, and kept that
    /// height over every later turn off the block (official client capture 2026-10-25T05-33-12 /
    /// 05-33-38, wired matrix action.4.B1b 1/1, action.4.B9b 4/4).
    /// </summary>
    public void Move(IRoomFloorItem item, int x, int y, Altitude? z, Rotation rotation)
    {
        var from = Where(item);

        SetFootprint(item, from, add: false);

        Altitude landing =
            from.X == x && from.Y == y && from.Rotation == rotation ? from.Z
            : from.X == x && from.Y == y ? TurnedInPlaceZ(item, from, rotation)
            : GetTile(map.ToIdx(x, y)).Height;
        var to = new WiredFurniPlacement(x, y, z ?? landing, rotation);

        _placements[item.ObjectId] = to;

        SetFootprint(item, to, add: true);
    }

    /// <summary>
    /// "Blocked by furni": any furni the movement physics addon's second source names standing on
    /// the tiles blocks the move, stackable or not. Furni the source leaves out stack as they
    /// always do (official client capture, 2026-10-14).
    /// </summary>
    private bool IsBlockedByFurni(
        IRoomFloorItem item,
        int x,
        int y,
        Rotation rotation,
        IWiredPolicy policy
    )
    {
        if (
            !policy.MovePhysics.HasFlag(WiredMovePhysicsFlags.BlockedByFurni)
            || !map.GetTileIdForSize(
                x,
                y,
                rotation,
                item.Definition.Width,
                item.Definition.Length,
                out var tileIds
            )
        )
            return false;

        var blocking = policy.BlockedByFurniIds;

        return tileIds.Any(idx =>
            GetFloorItemsOnTile(idx)
                .Any(other =>
                    !ReferenceEquals(other, item)
                    && (blocking?.Contains(other.ObjectId.Value) ?? true)
                )
        );
    }

    /// <summary>
    /// Where a furni turned in place stands: its own altitude, or the top of the highest tile its
    /// new footprint covers when that is higher. Called with the furni off its own tiles.
    /// </summary>
    public Altitude TurnedInPlaceZ(IRoomFloorItem item, WiredFurniPlacement from, Rotation rotation)
    {
        var z = from.Z;

        if (
            map.GetTileIdForSize(
                from.X,
                from.Y,
                rotation,
                item.Definition.Width,
                item.Definition.Length,
                out var tileIds
            )
        )
        {
            foreach (var idx in tileIds)
            {
                var height = GetTile(idx).Height;

                if (height > z)
                    z = height;
            }
        }

        return z;
    }

    private IEnumerable<IRoomFloorItem> GetFloorItemsOnTile(int tileIdx) =>
        _stacks.TryGetValue(tileIdx, out var stack) ? stack : furni.GetFloorItemsOnTile(tileIdx);

    private void SetFootprint(IRoomFloorItem item, WiredFurniPlacement placement, bool add)
    {
        if (
            !map.GetTileIdForSize(
                placement.X,
                placement.Y,
                placement.Rotation,
                item.Definition.Width,
                item.Definition.Length,
                out var tileIds
            )
        )
            return;

        foreach (var idx in tileIds)
        {
            if (!_stacks.TryGetValue(idx, out var stack))
                _stacks[idx] = stack = [.. furni.GetFloorItemsOnTile(idx)];

            if (add)
                stack.Add(item);
            else
                stack.Remove(item);
        }
    }
}
