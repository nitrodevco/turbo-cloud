using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomObjectModule(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    public Task<ImmutableDictionary<PlayerId, string>> GetAllOwnersAsync(CancellationToken ct) =>
        Task.FromResult(_roomGrain._state.OwnerNamesById.ToImmutableDictionary());

    public async Task<bool> AttatchObjectAsync(IRoomObject roomObject, CancellationToken ct)
    {
        switch (roomObject)
        {
            case IRoomItem item:
            {
                if (!_roomGrain._state.ItemsById.TryAdd(item.ObjectId, item))
                    throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

                if (!_roomGrain._state.OwnerNamesById.TryGetValue(item.OwnerId, out string? value))
                {
                    var ownerName = await _roomGrain
                        ._grainFactory.GetPlayerDirectoryGrain()
                        .GetPlayerNameAsync(item.OwnerId, ct);

                    value = ownerName;
                    _roomGrain._state.OwnerNamesById[item.OwnerId] = value;
                }

                item.SetOwnerName(value ?? string.Empty);
                item.SetAction(objectId => _roomGrain._state.DirtyItemIds.Add(objectId));

                if (!await AttatchLogicAsync(roomObject, ct) || !_roomGrain.MapModule.AddItem(item))
                    return false;
                break;
            }
            case IRoomAvatar avatar:
            {
                if (!_roomGrain._state.AvatarsByObjectId.TryAdd(avatar.ObjectId, avatar))
                    throw new TurboException(TurboErrorCodeEnum.AvatarNotFound);

                await AttatchLogicAsync(avatar, ct);
                await _roomGrain.AvatarModule.ProcessNextAvatarStepAsync(avatar, ct);

                _ = _roomGrain.SendComposerToRoomAsync(
                    new UsersMessageComposer { Avatars = [avatar.GetSnapshot()] }
                );
                break;
            }
            default:
                return false;
        }

        return true;
    }

    public async Task<bool> RemoveObjectAsync(
        ActionContext ctx,
        IRoomObject roomObject,
        CancellationToken ct,
        int pickerId = -1
    )
    {
        switch (roomObject)
        {
            case IRoomItem item:
            {
                if (!_roomGrain.MapModule.RemoveItem(item))
                    return false;

                await _roomGrain.SendComposerToRoomAsync(item.GetRemoveComposer(pickerId));

                await item.Logic.OnDetachAsync(ct);
                await item.Logic.OnPickupAsync(ctx, ct);

                item.SetAction(null);

                _roomGrain._state.ItemsById.Remove(item.ObjectId);

                var snapshot = item.GetSnapshot();

                await _roomGrain
                    ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
                    .EnqueueDirtyItemAsync(_roomGrain.RoomId, snapshot, ct, true);
                break;
            }
            case IRoomAvatar avatar:
            {
                await _roomGrain.AvatarModule.StopWalkingAsync(avatar, ct);

                _roomGrain.MapModule.RemoveAvatar(avatar, false);

                await avatar.Logic.OnDetachAsync(ct);

                await _roomGrain.SendComposerToRoomAsync(
                    new UserRemoveMessageComposer { ObjectId = avatar.ObjectId }
                );

                _roomGrain._state.AvatarsByObjectId.Remove(avatar.ObjectId);
                break;
            }
        }

        return true;
    }

    private async Task<bool> AttatchLogicAsync(IRoomObject roomObject, CancellationToken ct)
    {
        if (roomObject.Logic is not null)
            return false;

        var logicType = string.Empty;
        IRoomObjectContext? ctx = null;

        switch (roomObject)
        {
            case IRoomPlayer player:
                logicType = "default_avatar";
                ctx = new RoomPlayerContext(_roomGrain, player);
                break;
            case IRoomFloorItem floor:
                logicType = floor.Definition.LogicName;
                ctx = new RoomFloorItemContext(_roomGrain, floor);
                break;
            case IRoomWallItem wall:
                logicType = wall.Definition.LogicName;
                ctx = new RoomWallItemContext(_roomGrain, wall);
                break;
        }

        if (string.IsNullOrWhiteSpace(logicType) || ctx is null)
            return false;

        var logic = _roomGrain._logicProvider.CreateLogicInstance(logicType, ctx);

        roomObject.SetLogic(logic);

        await logic.OnAttachAsync(ct);

        return true;
    }
}
