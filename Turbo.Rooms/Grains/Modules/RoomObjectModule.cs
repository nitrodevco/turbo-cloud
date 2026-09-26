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
using Turbo.Rooms.Object.Avatars.Bot;
using Turbo.Rooms.Object.Avatars.Pet;
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

                // A temporary furni has no row to write, so its changes are not queued.
                if (!item.IsTemporary)
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

                _roomGrain.SendComposerToRoomAndForget(
                    new UsersMessageComposer { Avatars = [avatar.GetSnapshot()] }
                );
                break;
            }
            default:
                return false;
        }

        return true;
    }

    /// <param name="reportBorrow">
    /// False when the caller is letting go of many at once and will report the borrows it gave
    /// back itself, so the borrower hears one new total rather than one per furni.
    /// </param>
    public async Task<bool> RemoveObjectAsync(
        ActionContext ctx,
        IRoomObject roomObject,
        CancellationToken ct,
        int pickerId = -1,
        bool announce = true,
        bool reportBorrow = true
    )
    {
        switch (roomObject)
        {
            case IRoomItem item:
            {
                if (!_roomGrain.MapModule.RemoveItem(item))
                    return false;

                // A caller removing many items at once tells the room once for all of them.
                if (announce)
                    await _roomGrain.SendComposerToRoomAsync(item.GetRemoveComposer(pickerId), ct);

                await item.Logic.OnDetachAsync(ct);
                await item.Logic.OnPickupAsync(ctx, ct);

                item.SetAction(null);

                _roomGrain._state.ItemsById.Remove(item.ObjectId);

                if (item.IsBuildersClub)
                {
                    // Nobody owns a borrowed furni, so there is nowhere for it to go: leaving the
                    // room is the end of it, and the borrow goes back to the club.
                    await _roomGrain
                        ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
                        .EnqueueDeletedItemAsync(_roomGrain.RoomId, item.ObjectId, ct);

                    if (reportBorrow)
                        await _roomGrain
                            ._grainFactory.GetBuildersClubGrain()
                            .OnReturnedAsync(item.OwnerId, 1, ct);
                }
                else if (!item.IsTemporary)
                {
                    await _roomGrain
                        ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
                        .EnqueueDirtyItemAsync(_roomGrain.RoomId, item.GetSnapshot(), ct, true);
                }

                break;
            }
            case IRoomAvatar avatar:
            {
                await _roomGrain.AvatarModule.StopWalkingAsync(avatar, ct);

                _roomGrain.MapModule.RemoveAvatar(avatar, false);

                await avatar.Logic.OnDetachAsync(ct);

                await _roomGrain.SendComposerToRoomAsync(
                    new UserRemoveMessageComposer { ObjectId = avatar.ObjectId },
                    ct
                );

                _roomGrain._state.AvatarsByObjectId.Remove(avatar.ObjectId);
                break;
            }
        }

        return true;
    }

    private async Task<bool> AttatchLogicAsync(IRoomObject roomObject, CancellationToken ct)
    {
        if (!EnsureLogic(roomObject))
            return false;

        await roomObject.Logic.OnAttachAsync(ct);

        return true;
    }

    /// <summary>
    /// Gives an object its logic without attaching it: nothing hears of it and the room does
    /// not change. Placement uses it so the room's limits can tell what kind of furni a new item
    /// is (<see cref="RoomFurniModule.EnsureWithinPlacementLimits"/>) before anything is done
    /// that would have to be undone. Attaching later keeps the logic made here.
    /// </summary>
    internal bool EnsureLogic(IRoomObject roomObject)
    {
        if (roomObject.Logic is not null)
            return true;

        var logicType = string.Empty;
        IRoomObjectContext? ctx = null;

        switch (roomObject)
        {
            case IRoomPlayer player:
                logicType = "default_avatar";
                ctx = new RoomPlayerContext(_roomGrain, player);
                break;
            case IRoomPet pet:
                logicType = "default_pet";
                ctx = new RoomPetContext(_roomGrain, pet);
                break;
            case IRoomBot bot:
                logicType = "default_bot";
                ctx = new RoomBotContext(_roomGrain, bot);
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

        roomObject.SetLogic(_roomGrain._logicProvider.CreateLogicInstance(logicType, ctx));

        return true;
    }
}
