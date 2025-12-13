using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
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
)
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomMapModule _roomMap = roomMapModule;

    private int _tickCount = 0;

    public async Task ProcessRollersAsync(CancellationToken ct)
    {
        _tickCount++;

        if (_tickCount != _roomConfig.RoomRollerTickCount)
            return;

        _tickCount = 0;

        var rollers = _state
            .FloorItemsById.Values.Where(x => x.Logic is FurnitureRollerLogic)
            .ToList();

        if (rollers.Count == 0)
            return;

        var plans = new List<RollerMovePlan>();
        var furniChanges = new List<(int, int, double)>();
        var composers = new List<IComposer>();

        foreach (var group in rollers.GroupBy(x => x.Rotation))
        {
            var stack = OrderRollersFrontToBack(group);

            foreach (var roller in stack)
            {
                try
                {
                    if (roller is null)
                        continue;

                    var fromIdx = _roomMap.ToIdx(roller.X, roller.Y);

                    if (!_roomMap.TryGetTileInFront(fromIdx, roller.Rotation, out var toIdx))
                        continue;

                    var toTileState = _state.TileFlags[toIdx];
                    var toTileHeight = _state.TileHeights[toIdx];

                    if (toTileHeight > roller.Height || IsAvatarAboutToWalkHere(toIdx))
                        continue;

                    var floorItems = _state
                        .TileFloorStacks[fromIdx]
                        .Select(x => _state.FloorItemsById[x])
                        .Where(x =>
                            x.ObjectId.Value != roller.ObjectId.Value
                            && x.Z >= roller.Height
                            && x.Logic.CanRoll()
                        )
                        .ToList();
                    var avatars = _state
                        .TileAvatarStacks[fromIdx]
                        .Select(x => _state.AvatarsByObjectId[x])
                        .Where(x => !x.IsWalking && x.Z >= roller.Height)
                        .ToList();

                    if (
                        floorItems.Count == 0 && avatars.Count == 0
                        || (floorItems.Count > 0 && toTileState.Has(RoomTileFlags.StackBlocked))
                        || (avatars.Count > 0 && toTileState.Has(RoomTileFlags.Closed))
                    )
                        continue;

                    var rollingFurni = new List<(int, double, double)>();

                    foreach (var item in floorItems)
                    {
                        var nextZ = item.Z - roller.Height + toTileHeight;

                        furniChanges.Add((item.ObjectId.Value, toIdx, nextZ));
                        rollingFurni.Add((item.ObjectId.Value, item.Z, nextZ));
                    }

                    foreach (var avatar in avatars)
                    {
                        var nextZ = avatar.Z - roller.Height + toTileHeight;

                        furniChanges.Add((avatar.ObjectId.Value, toIdx, nextZ));
                        rollingFurni.Add((avatar.ObjectId.Value, avatar.Z, nextZ));
                    }

                    if (avatars.Count > 0)
                    {
                        var sent = false;

                        foreach (var avatar in avatars)
                        {
                            if (!sent)
                            {
                                composers.Add(
                                    new SlideObjectBundleMessageComposer
                                    {
                                        FromX = roller.X,
                                        FromY = roller.Y,
                                        ToX = _roomMap.GetX(toIdx),
                                        ToY = _roomMap.GetY(toIdx),
                                        RollerItemId = roller.ObjectId.Value,
                                        FloorItemHeights = [.. rollingFurni],
                                        Avatar = (
                                            SlideAvatarMoveType.Slide,
                                            avatar.ObjectId.Value,
                                            avatar.Z,
                                            avatar.Z - roller.Height + toTileHeight
                                        ),
                                    }
                                );

                                sent = true;

                                continue;
                            }

                            composers.Add(
                                new SlideObjectBundleMessageComposer
                                {
                                    FromX = roller.X,
                                    FromY = roller.Y,
                                    ToX = _roomMap.GetX(toIdx),
                                    ToY = _roomMap.GetY(toIdx),
                                    RollerItemId = roller.ObjectId.Value,
                                    FloorItemHeights = [],
                                    Avatar = (
                                        SlideAvatarMoveType.Slide,
                                        avatar.ObjectId.Value,
                                        avatar.Z,
                                        avatar.Z - roller.Height + toTileHeight
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
                                FromX = roller.X,
                                FromY = roller.Y,
                                ToX = _roomMap.GetX(toIdx),
                                ToY = _roomMap.GetY(toIdx),
                                RollerItemId = roller.ObjectId.Value,
                                FloorItemHeights = [.. rollingFurni],
                                Avatar = null,
                            }
                        );
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        if (composers.Count == 0)
            return;

        foreach (var (furniId, toIdx, toZ) in furniChanges)
        {
            if (_state.FloorItemsById.TryGetValue(furniId, out var item))
            {
                _roomMap.RollFloorItem(item, toIdx, toZ, out _);
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
