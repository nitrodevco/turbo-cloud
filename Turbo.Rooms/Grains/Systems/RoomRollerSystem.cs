using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains.Systems;

public sealed class RoomRollerSystem(RoomGrain roomGrain)
    : RoomGrainComponent(roomGrain),
        IRoomEventListener
{
    private readonly List<List<int>> _rollerIdSets = [];

    // Per-tick scratch, reused: the tiles taken this tick, the tiles avatars are stepping onto,
    // and what one roller carries.
    private readonly HashSet<int> _reservedTileIdxs = [];
    private readonly HashSet<int> _nextAvatarTiles = [];
    private readonly List<IRoomItem> _items = [];
    private readonly List<IRoomAvatar> _avatars = [];

    private bool _isDirtyRollers = true;

    public Task ProcessRollersAsync(long now, CancellationToken ct)
    {
        if (now < _roomGrain._state.NextRollerBoundaryMs)
            return Task.CompletedTask;
        while (now >= _roomGrain._state.NextRollerBoundaryMs)
            _roomGrain._state.NextRollerBoundaryMs += _roomGrain._roomConfig.RollerTickMs;
        ComputeRollers();

        if (_rollerIdSets.Count == 0)
            return Task.CompletedTask;
        var currentPlans = new List<RollerMovePlanSnapshot>();

        _reservedTileIdxs.Clear();
        _nextAvatarTiles.Clear();

        foreach (var avatar in AvatarModule.Avatars)
        {
            if (avatar.NextTileId >= 0)
                _nextAvatarTiles.Add(avatar.NextTileId);
        }

        foreach (var rollerIds in _rollerIdSets)
        {
            if (rollerIds.Count == 0)
                continue;

            foreach (var rollerId in rollerIds)
            {
                try
                {
                    if (!FurniModule.TryGetItem(rollerId, out var roller))
                        continue;

                    var fromIdx = MapModule.ToIdx(roller.X, roller.Y);

                    if (
                        !MapModule.TryGetTileInFront(fromIdx, roller.Rotation, out var toIdx)
                        || fromIdx == toIdx
                        || _reservedTileIdxs.Contains(toIdx)
                        || _nextAvatarTiles.Contains(toIdx)
                    )
                        continue;

                    var toTileState = _roomGrain._state.TileFlags[toIdx];
                    var toTileHeight = _roomGrain._state.TileHeights[toIdx];
                    var rollerHeight = roller.Height;

                    if (
                        toTileHeight > rollerHeight
                        || toTileState.Has(RoomTileFlags.AvatarOccupied, RoomTileFlags.Disabled)
                    )
                        continue;

                    // Kept across rollers and ticks: most rollers carry nothing on most ticks.
                    var items = _items;
                    var avatars = _avatars;
                    var canAvatarMove = true;

                    items.Clear();
                    avatars.Clear();

                    foreach (var itemId in _roomGrain._state.TileFloorStacks[fromIdx])
                    {
                        if (
                            !FurniModule.TryGetItem(itemId, out var item)
                            || item.Definition.Width > 1
                            || item.Definition.Length > 1
                            || item.Z < rollerHeight
                            || !item.Logic.CanRoll()
                            || toTileState.Has(RoomTileFlags.StackBlocked)
                        )
                            continue;

                        items.Add(item);
                    }

                    foreach (var avatarId in _roomGrain._state.TileAvatarStacks[fromIdx])
                    {
                        if (
                            !AvatarModule.TryGetAvatar(avatarId, out var avatar)
                            || avatar.Z < rollerHeight
                        )
                            continue;

                        if (!avatar.Logic.CanRoll() || !MapModule.CanAvatarWalk(avatar, toIdx))
                        {
                            canAvatarMove = false;

                            break;
                        }

                        avatars.Add(avatar);
                    }

                    if (!canAvatarMove || (items.Count == 0 && avatars.Count == 0))
                        continue;

                    currentPlans.Add(
                        new RollerMovePlanSnapshot
                        {
                            RollerId = roller.ObjectId,
                            FromIdx = fromIdx,
                            ToIdx = toIdx,
                            MovedFloorItems =
                            [
                                .. items.Select(x => new RollerMovedObjectSnapshot
                                {
                                    ObjectId = x.ObjectId,
                                    RoomObject = x,
                                    FromZ = x.Z,
                                    ToZ = x.Z - rollerHeight + toTileHeight,
                                }),
                            ],
                            MovedAvatars =
                            [
                                .. avatars.Select(x =>
                                {
                                    return new RollerMovedObjectSnapshot
                                    {
                                        ObjectId = x.ObjectId,
                                        RoomObject = x,
                                        FromZ = x.Z,
                                        ToZ = x.Z - rollerHeight + toTileHeight,
                                    };
                                }),
                            ],
                        }
                    );

                    _reservedTileIdxs.Add(toIdx);
                }
                catch (Exception ex)
                {
                    // One roller that cannot be planned must not stop the others this tick.
                    _roomGrain._logger.LogError(
                        ex,
                        "Roller {RollerId} in room {RoomId} failed to plan its move",
                        rollerId,
                        _roomGrain.RoomId
                    );
                }
            }
        }

        if (currentPlans.Count == 0)
            return Task.CompletedTask;
        var composers = ImmutableArray.CreateBuilder<IComposer>();

        foreach (var plan in currentPlans)
        {
            var (fromX, fromY) = MapModule.GetTileXY(plan.FromIdx);
            var (toX, toY) = MapModule.GetTileXY(plan.ToIdx);

            foreach (var item in plan.MovedFloorItems)
                MapModule.RollFloorItem((IRoomFloorItem)item.RoomObject, plan.ToIdx, item.ToZ);
            foreach (var avatar in plan.MovedAvatars)
                MapModule.RollAvatar((IRoomAvatar)avatar.RoomObject, plan.ToIdx, avatar.ToZ);

            // The furni ride with the first avatar's packet, or alone when nobody is on the
            // roller; each further avatar gets a packet of its own.
            if (plan.MovedAvatars.Count == 0)
                composers.Add(CreateSlideComposer(plan, fromX, fromY, toX, toY, true, null));

            for (var i = 0; i < plan.MovedAvatars.Count; i++)
                composers.Add(
                    CreateSlideComposer(plan, fromX, fromY, toX, toY, i == 0, plan.MovedAvatars[i])
                );
        }

        // Every slide of the tick as one room message, in the order the moves were made.
        _roomGrain.SendComposersToRoomAndForget(composers.ToImmutable());
        return Task.CompletedTask;
    }

    /// <summary>One slide packet of a roller's move: its furni when asked for, and one avatar or none.</summary>
    private static SlideObjectBundleMessageComposer CreateSlideComposer(
        RollerMovePlanSnapshot plan,
        int fromX,
        int fromY,
        int toX,
        int toY,
        bool withFurni,
        RollerMovedObjectSnapshot? avatar
    ) =>
        new()
        {
            FromX = fromX,
            FromY = fromY,
            ToX = toX,
            ToY = toY,
            RollerItemId = plan.RollerId,
            FloorItemHeights = withFurni
                ? [.. plan.MovedFloorItems.Select(x => (x.RoomObject.ObjectId, x.FromZ, x.ToZ))]
                : [],
            Avatar = avatar is null
                ? null
                : (
                    SlideAvatarMoveType.Slide,
                    avatar.RoomObject.ObjectId,
                    avatar.FromZ + ((IRoomAvatar)avatar.RoomObject).PostureOffset,
                    avatar.ToZ + ((IRoomAvatar)avatar.RoomObject).PostureOffset
                ),
        };

    private void ComputeRollers()
    {
        if (!_isDirtyRollers || !_roomGrain._state.IsFurniLoaded)
            return;

        _rollerIdSets.Clear();

        // Clean before the room is known to have none: a room without rollers used to return
        // early, stay dirty, and scan every item again on every roller tick for good.
        _isDirtyRollers = false;

        var rollers = FurniModule.Items.Where(x => x.Logic is FurnitureRollerLogic).ToList();

        if (rollers.Count == 0)
            return;

        foreach (var group in rollers.GroupBy(x => x.Rotation).OrderBy(x => x.Key))
        {
            var stack = OrderRollersFrontToBack(group);

            _rollerIdSets.Add([.. stack.Select(x => x.ObjectId)]);
        }
    }

    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct) =>
        HandleRoomEventAsync(evt, ct);

    private Task HandleRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case RoomRollerChangedEvent rollerChangedEvent:
                _isDirtyRollers = true;
                break;
            default:
                return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private static IEnumerable<IRoomItem> OrderRollersFrontToBack(IEnumerable<IRoomItem> rollers)
    {
        var list = rollers.ToList();

        if (list.Count == 0)
            return list;

        var dir = list[0].Rotation;

        return dir switch
        {
            Rotation.East => list.OrderByDescending(r => r.X).ThenBy(r => r.Y),
            Rotation.West => list.OrderBy(r => r.X).ThenBy(r => r.Y),
            Rotation.South => list.OrderByDescending(r => r.Y).ThenBy(r => r.X),
            Rotation.North => list.OrderBy(r => r.Y).ThenBy(r => r.X),
            _ => list.OrderBy(r => r.Y).ThenBy(r => r.X),
        };
    }
}
