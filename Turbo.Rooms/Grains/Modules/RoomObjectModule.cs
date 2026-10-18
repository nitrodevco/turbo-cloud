using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
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

public sealed partial class RoomObjectModule(RoomGrain roomGrain) : RoomGrainComponent(roomGrain)
{
    public async Task<bool> AttatchObjectAsync(IRoomObject roomObject, CancellationToken ct)
    {
        switch (roomObject)
        {
            case IRoomItem item:
            {
                if (!_roomGrain._state.ItemsById.TryAdd(item.ObjectId, item))
                    throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

                _roomGrain._state.ItemsVersion++;

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

                if (!await AttatchLogicAsync(roomObject, ct) || !MapModule.AddItem(item))
                    return false;
                break;
            }
            case IRoomAvatar avatar:
            {
                if (!_roomGrain._state.AvatarsByObjectId.TryAdd(avatar.ObjectId, avatar))
                    throw new TurboException(TurboErrorCodeEnum.AvatarNotFound);

                if (avatar is IRoomPet pet)
                    _roomGrain._state.Pets.Add(pet);
                else if (avatar is IRoomBot bot)
                    _roomGrain._state.Bots.Add(bot);

                await AttatchLogicAsync(avatar, ct);
                await AvatarModule.ProcessNextAvatarStepAsync(avatar, ct);

                var users = new UsersMessageComposer { Avatars = [avatar.GetSnapshot()] };

                // A player walking in is sent their own avatar first thing in the rest of their
                // entry (RoomGrain.BuildEntryTailAsync); through the room it would reach them
                // whenever the stream does, after the entry rather than at its head.
                if (avatar is IRoomPlayer player)
                    _roomGrain
                        .SendComposerToRoomExceptAsync(
                            users,
                            [player.PlayerId],
                            CancellationToken.None
                        )
                        .LogAndForget(
                            _roomGrain._logger,
                            "send a new avatar to room {RoomId}",
                            _roomGrain._state.RoomId
                        );
                else
                    _roomGrain.SendComposerToRoomAndForget(users);
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
    /// <param name="removeDelayMs">How long the client waits before it takes the item away.</param>
    /// <param name="toldFirst">
    /// A player in the room who is sent something of their own straight after (the picked-up
    /// item in their inventory): they are told of the removal and the heights it changed
    /// directly, so what follows reaches them after it; through the room it would come later.
    /// </param>
    public async Task<bool> RemoveObjectAsync(
        ActionContext ctx,
        IRoomObject roomObject,
        CancellationToken ct,
        int pickerId = -1,
        bool announce = true,
        bool reportBorrow = true,
        int removeDelayMs = 0,
        PlayerId? toldFirst = null
    )
    {
        switch (roomObject)
        {
            case IRoomItem item:
            {
                if (!MapModule.RemoveItem(item))
                    return false;

                // A caller removing many items at once tells the room once for all of them.
                if (announce && toldFirst is { } playerId && playerId > 0)
                {
                    ImmutableArray<IComposer> composers =
                    [
                        item.GetRemoveComposer(pickerId, delay: removeDelayMs),
                        .. _roomGrain.TakeHeightMapUpdates(),
                    ];

                    await _roomGrain.SendComposersToRoomExceptAsync(composers, [playerId], ct);

                    foreach (var composer in composers)
                        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                            playerId,
                            composer,
                            ct
                        );
                }
                else if (announce)
                    await _roomGrain.SendComposerToRoomAsync(
                        item.GetRemoveComposer(pickerId, delay: removeDelayMs),
                        ct
                    );

                await item.Logic.OnDetachAsync(ct);
                await item.Logic.OnPickupAsync(ctx, ct);

                item.SetAction(null);

                _roomGrain._state.ItemsById.Remove(item.ObjectId);
                _roomGrain._state.ItemsVersion++;

                // Nobody owns a borrowed furni, so leaving the room is the end of it, and the
                // borrow goes back to the club.
                if (item.IsBuildersClub && reportBorrow)
                    await _roomGrain
                        ._grainFactory.GetBuildersClubGrain()
                        .OnReturnedAsync(item.OwnerId, [item.Definition.LogicName], ct);

                // Where a furni goes is the caller's to write, with why: an inventory (a pickup,
                // ReturnItemsToOwnersAsync), back to the club (a borrowed one) or nowhere (a use
                // that deletes it).

                break;
            }
            case IRoomAvatar avatar:
            {
                await AvatarModule.StopWalkingAsync(avatar, ct);

                MapModule.RemoveAvatar(avatar, false);

                await avatar.Logic.OnDetachAsync(ct);

                await _roomGrain.SendComposerToRoomAsync(
                    new UserRemoveMessageComposer { ObjectId = avatar.ObjectId },
                    ct
                );

                _roomGrain._state.AvatarsByObjectId.Remove(avatar.ObjectId);

                if (avatar is IRoomPet pet)
                    _roomGrain._state.Pets.Remove(pet);
                else if (avatar is IRoomBot bot)
                    _roomGrain._state.Bots.Remove(bot);
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
