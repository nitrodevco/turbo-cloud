using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains.Systems;

internal sealed class RoomRollerSystem(
    RoomGrain roomGrain,
    RoomConfig roomConfig,
    RoomLiveState roomLiveState,
    RoomMapModule roomMapModule
) : IRoomEventListener
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomMapModule _roomMap = roomMapModule;

    private readonly List<List<int>> _rollerIdSets = [];

    private bool _isDirtyRollers = true;
    private int _tickMs => _roomConfig.RollerTickMs;

    public async Task ProcessRollersAsync(long now, CancellationToken ct)
    {
        if (now < _state.NextRollerBoundaryMs)
            return;

        while (now >= _state.NextRollerBoundaryMs)
            _state.NextRollerBoundaryMs += _tickMs;

        ComputeRollers();

        if (_rollerIdSets.Count == 0)
            return;

        var currentPlans = new List<RollerMovePlan>();
        var reservedTileIdxs = new HashSet<int>();
        var nextAvatarTiles = new HashSet<int>(
            _state.AvatarsByObjectId.Values.Where(x => x.NextTileId >= 0).Select(x => x.NextTileId)
        );

        foreach (var rollerIds in _rollerIdSets)
        {
            if (rollerIds.Count == 0)
                continue;

            foreach (var rollerId in rollerIds)
            {
                try
                {
                    if (!_state.FloorItemsById.TryGetValue(rollerId, out var roller))
                        continue;

                    var fromIdx = _roomMap.ToIdx(roller.X, roller.Y);

                    if (
                        !_roomMap.TryGetTileInFront(fromIdx, roller.Rotation, out var toIdx)
                        || fromIdx == toIdx
                        || reservedTileIdxs.Contains(toIdx)
                        || nextAvatarTiles.Contains(toIdx)
                    )
                        continue;

                    var toTileState = _state.TileFlags[toIdx];
                    var toTileHeight = _state.TileHeights[toIdx];
                    var rollerHeight = roller.Z + roller.GetStackHeight();

                    if (
                        toTileHeight > rollerHeight
                        || toTileState.Has(RoomTileFlags.AvatarOccupied, RoomTileFlags.Disabled)
                    )
                        continue;

                    var floorItems = new List<IRoomFloorItem>();
                    var avatars = new List<IRoomAvatar>();
                    var canAvatarMove = true;

                    foreach (var itemId in _state.TileFloorStacks[fromIdx])
                    {
                        if (
                            !_state.FloorItemsById.TryGetValue(itemId, out var item)
                            || item.Definition.Width > 1
                            || item.Definition.Length > 1
                            || item.Z < rollerHeight
                            || !item.Logic.CanRoll()
                        )
                            continue;

                        floorItems.Add(item);
                    }

                    foreach (var avatarId in _state.TileAvatarStacks[fromIdx])
                    {
                        if (
                            !_state.AvatarsByObjectId.TryGetValue(avatarId, out var avatar)
                            || avatar.Z < rollerHeight
                        )
                            continue;

                        if (!avatar.Logic.CanRoll())
                        {
                            canAvatarMove = false;
                            break;
                        }

                        avatars.Add(avatar);
                    }

                    if (!canAvatarMove)
                        continue;

                    if (
                        (floorItems.Count == 0 && avatars.Count == 0)
                        || (floorItems.Count > 0 && toTileState.Has(RoomTileFlags.StackBlocked))
                        || (avatars.Count > 0 && toTileState.Has(RoomTileFlags.Closed))
                    )
                        continue;

                    currentPlans.Add(
                        new RollerMovePlan
                        {
                            RollerId = roller.ObjectId,
                            FromIdx = fromIdx,
                            ToIdx = toIdx,
                            MovedFloorItems =
                            [
                                .. floorItems.Select(x => new RollerMovedObject
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
                                    return new RollerMovedObject
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

                    reservedTileIdxs.Add(toIdx);
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        if (currentPlans.Count == 0)
            return;

        var composers = new List<IComposer>();

        foreach (var plan in currentPlans)
        {
            var (fromX, fromY) = _roomMap.GetTileXY(plan.FromIdx);
            var (toX, toY) = _roomMap.GetTileXY(plan.ToIdx);

            foreach (var item in plan.MovedFloorItems)
                _roomMap.RollFloorItem((IRoomFloorItem)item.RoomObject, plan.ToIdx, item.ToZ);

            foreach (var avatar in plan.MovedAvatars)
                _roomMap.RollAvatar((IRoomAvatar)avatar.RoomObject, plan.ToIdx, avatar.ToZ);

            if (plan.MovedAvatars.Count > 0)
            {
                var sent = false;

                foreach (var avatar in plan.MovedAvatars)
                {
                    var avatarObject = (IRoomAvatar)avatar.RoomObject;

                    if (!sent)
                    {
                        composers.Add(
                            new SlideObjectBundleMessageComposer
                            {
                                FromX = fromX,
                                FromY = fromY,
                                ToX = toX,
                                ToY = toY,
                                RollerItemId = plan.RollerId,
                                FloorItemHeights =
                                [
                                    .. plan.MovedFloorItems.Select(x =>
                                        (x.RoomObject.ObjectId, x.FromZ, x.ToZ)
                                    ),
                                ],
                                Avatar = (
                                    SlideAvatarMoveType.Slide,
                                    avatar.RoomObject.ObjectId,
                                    avatar.FromZ + avatarObject.PostureOffset,
                                    avatar.ToZ + avatarObject.PostureOffset
                                ),
                            }
                        );

                        sent = true;

                        continue;
                    }

                    composers.Add(
                        new SlideObjectBundleMessageComposer
                        {
                            FromX = fromX,
                            FromY = fromY,
                            ToX = toX,
                            ToY = toY,
                            RollerItemId = plan.RollerId,
                            FloorItemHeights = [],
                            Avatar = (
                                SlideAvatarMoveType.Slide,
                                avatar.RoomObject.ObjectId,
                                avatar.FromZ + avatarObject.PostureOffset,
                                avatar.ToZ + avatarObject.PostureOffset
                            ),
                        }
                    );
                }
            }
            else
            {
                composers.Add(
                    new SlideObjectBundleMessageComposer
                    {
                        FromX = fromX,
                        FromY = fromY,
                        ToX = toX,
                        ToY = toY,
                        RollerItemId = plan.RollerId,
                        FloorItemHeights =
                        [
                            .. plan.MovedFloorItems.Select(x =>
                                (x.RoomObject.ObjectId, x.FromZ, x.ToZ)
                            ),
                        ],
                        Avatar = null,
                    }
                );
            }
        }

        foreach (var composer in composers)
            _ = _roomGrain.SendComposerToRoomAsync(composer);
    }

    private void ComputeRollers()
    {
        if (!_isDirtyRollers || !_state.IsFurniLoaded)
            return;

        _rollerIdSets.Clear();

        var rollers = _state
            .FloorItemsById.Values.Where(x => x.Logic is FurnitureRollerLogic)
            .ToList();

        if (rollers.Count == 0)
            return;

        foreach (var group in rollers.GroupBy(x => x.Rotation).OrderBy(x => x.Key))
        {
            var stack = OrderRollersFrontToBack(group);

            _rollerIdSets.Add([.. stack.Select(x => x.ObjectId)]);
        }

        _isDirtyRollers = false;
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

    private static IEnumerable<IRoomFloorItem> OrderRollersFrontToBack(
        IEnumerable<IRoomFloorItem> rollers
    )
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
