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

    private int _tickCount = 0;

    private Dictionary<Rotation, List<int>> _rollerIdsByDirection = [];
    private List<RollerMovePlan> _currentPlans = [];

    private bool _isDirtyRollers = true;

    public async Task ProcessRollersAsync(CancellationToken ct)
    {
        _tickCount++;

        if (_currentPlans.Count > 0)
        {
            foreach (var plan in _currentPlans)
            {
                var avatarIds = plan
                    .MovedAvatars.Select(x => x.RoomObject.ObjectId.Value)
                    .ToHashSet();

                await _roomMap.InvokeAvatarsAsync(avatarIds, ct);
            }

            _currentPlans.Clear();
        }

        if (_tickCount != _roomConfig.RoomRollerTickCount)
            return;

        _tickCount = 0;

        ComputeRollers();

        if (_rollerIdsByDirection.Count == 0)
            return;

        _currentPlans.Clear();

        foreach (var (rotation, rollerIds) in _rollerIdsByDirection)
        {
            var rollers = rollerIds.Select(x => _state.FloorItemsById[x]).ToList();

            foreach (var roller in rollers)
            {
                try
                {
                    if (roller is null)
                        continue;

                    var fromIdx = _roomMap.ToIdx(roller.X, roller.Y);

                    if (
                        !_roomMap.TryGetTileInFront(fromIdx, roller.Rotation, out var toIdx)
                        || fromIdx == toIdx
                    )
                        continue;

                    var fromTileState = _state.TileFlags[fromIdx];
                    var toTileState = _state.TileFlags[toIdx];
                    var toTileHeight = _state.TileHeights[toIdx];
                    var rollerHeight = roller.Z + roller.Height;

                    if (
                        toTileHeight > rollerHeight
                        || toTileState.Has(RoomTileFlags.AvatarOccupied)
                        || _currentPlans.Any(x => x.ToIdx == toIdx)
                        || IsAvatarAboutToWalkHere(toIdx)
                    )
                        continue;

                    var floorItems = _state
                        .TileFloorStacks[fromIdx]
                        .Select(x => _state.FloorItemsById[x])
                        .Where(x => x.Z >= rollerHeight && x.Logic.CanRoll())
                        .ToList();
                    var avatars = _state
                        .TileAvatarStacks[fromIdx]
                        .Select(x => _state.AvatarsByObjectId[x])
                        .Where(x => x.Z >= rollerHeight && x.Logic.CanRoll())
                        .ToList();

                    if (
                        floorItems.Count == 0 && avatars.Count == 0
                        || floorItems.Count > 0 && toTileState.Has(RoomTileFlags.StackBlocked)
                        || avatars.Count > 0 && toTileState.Has(RoomTileFlags.Closed)
                    )
                        continue;

                    _currentPlans.Add(
                        new RollerMovePlan
                        {
                            RollerId = roller.ObjectId.Value,
                            FromIdx = fromIdx,
                            ToIdx = toIdx,
                            MovedFloorItems =
                            [
                                .. floorItems.Select(x => new RollerMovedEntity
                                {
                                    ObjectId = x.ObjectId,
                                    RoomObject = x,
                                    ZOffset = 0,
                                    FromZ = x.Z,
                                    ToZ = x.Z - rollerHeight + toTileHeight,
                                }),
                            ],
                            MovedAvatars =
                            [
                                .. avatars.Select(x =>
                                {
                                    var offset = 0.0;

                                    if (x.HasStatus(AvatarStatusType.Sit))
                                    {
                                        offset = double.Parse(x.Statuses[AvatarStatusType.Sit]);
                                    }
                                    else if (x.HasStatus(AvatarStatusType.Lay))
                                    {
                                        offset = double.Parse(x.Statuses[AvatarStatusType.Lay]);
                                    }

                                    return new RollerMovedEntity
                                    {
                                        ObjectId = x.ObjectId,
                                        RoomObject = x,
                                        ZOffset = offset,
                                        FromZ = x.Z,
                                        ToZ = x.Z - rollerHeight + toTileHeight,
                                    };
                                }),
                            ],
                        }
                    );
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        if (_currentPlans.Count == 0)
            return;

        var composers = new List<IComposer>();
        var updatedAvatarIds = new HashSet<int>();

        foreach (var plan in _currentPlans)
        {
            var (fromX, fromY) = _roomMap.GetTileXY(plan.FromIdx);
            var (toX, toY) = _roomMap.GetTileXY(plan.ToIdx);

            foreach (var item in plan.MovedFloorItems)
            {
                _roomMap.RollFloorItem(
                    (IRoomFloorItem)item.RoomObject,
                    plan.ToIdx,
                    item.ToZ,
                    out _
                );
            }

            if (plan.MovedAvatars.Count > 0)
            {
                var sent = false;

                foreach (var avatar in plan.MovedAvatars)
                {
                    var avatarObject = (IRoomAvatar)avatar.RoomObject;

                    _roomMap.RollAvatar(avatarObject, plan.ToIdx, avatar.ToZ);

                    updatedAvatarIds.Add(avatarObject.ObjectId.Value);

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
                                        (x.RoomObject.ObjectId.Value, x.FromZ, x.ToZ)
                                    ),
                                ],
                                Avatar = (
                                    SlideAvatarMoveType.Slide,
                                    avatar.RoomObject.ObjectId.Value,
                                    avatar.FromZ + avatar.ZOffset,
                                    avatar.ToZ + avatar.ZOffset
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
                                avatar.RoomObject.ObjectId.Value,
                                avatar.FromZ,
                                avatar.ToZ
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
                                (x.RoomObject.ObjectId.Value, x.FromZ, x.ToZ)
                            ),
                        ],
                        Avatar = null,
                    }
                );
            }
        }

        foreach (var composer in composers)
        {
            _ = _roomGrain.SendComposerToRoomAsync(composer, ct);
        }
    }

    private bool IsAvatarAboutToWalkHere(int tileIdx)
    {
        foreach (var avatar in _state.AvatarsByObjectId.Values)
        {
            if (avatar.NextTileId == tileIdx)
                return true;
        }

        return false;
    }

    private void ComputeRollers()
    {
        if (!_isDirtyRollers)
            return;

        _rollerIdsByDirection.Clear();

        var rollers = _state
            .FloorItemsById.Values.Where(x => x.Logic is FurnitureRollerLogic)
            .ToList();

        if (rollers.Count == 0)
            return;

        foreach (var group in rollers.GroupBy(x => x.Rotation))
        {
            var stack = OrderRollersFrontToBack(group);

            _rollerIdsByDirection.TryAdd(group.Key, [.. stack.Select(x => x.ObjectId.Value)]);
        }

        _isDirtyRollers = false;
    }

    public Task OnRoomEventAsync(RoomEvent @event, CancellationToken ct) =>
        HandleRoomEventAsync(@event, ct);

    private Task HandleRoomEventAsync(RoomEvent @event, CancellationToken ct)
    {
        switch (@event)
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

        // All rollers in this group should share the same direction.
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

    public static IEnumerable<IRoomFloorItem> OrderRollersBackToFront(
        IEnumerable<IRoomFloorItem> rollers
    )
    {
        var list = rollers.ToList();

        if (list.Count == 0)
            return list;

        var dir = list[0].Rotation;

        return dir switch
        {
            Rotation.East => list.OrderBy(r => r.X).ThenBy(r => r.Y),
            Rotation.West => list.OrderByDescending(r => r.X).ThenBy(r => r.Y),
            Rotation.South => list.OrderBy(r => r.Y).ThenBy(r => r.X),
            Rotation.North => list.OrderByDescending(r => r.Y).ThenBy(r => r.X),
            _ => list.OrderBy(r => r.Y).ThenBy(r => r.X),
        };
    }
}
