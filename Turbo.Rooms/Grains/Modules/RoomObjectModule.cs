using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
using Turbo.Primitives.Rooms.Providers;
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
                if (!State.ItemsById.TryAdd(item.ObjectId, item))
                    throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

                State.ItemsVersion++;

                if (!State.OwnerNamesById.TryGetValue(item.OwnerId, out string? value))
                {
                    var ownerName = await _roomGrain
                        ._grainFactory.GetPlayerDirectoryGrain()
                        .GetPlayerNameAsync(item.OwnerId, ct);

                    value = ownerName;
                    State.OwnerNamesById[item.OwnerId] = value;
                }

                item.SetOwnerName(value ?? string.Empty);

                // A temporary furni has no row to write, so its changes are not queued.
                if (!item.IsTemporary)
                    item.SetAction(objectId => State.DirtyItemIds.Add(objectId));

                if (!await AttatchLogicAsync(roomObject, ct) || !MapModule.AddItem(item))
                    return false;
                break;
            }
            case IRoomAvatar avatar:
            {
                if (!State.AvatarsByObjectId.TryAdd(avatar.ObjectId, avatar))
                    throw new TurboException(TurboErrorCodeEnum.AvatarNotFound);

                if (avatar is IRoomPet pet)
                    State.Pets.Add(pet);
                else if (avatar is IRoomBot bot)
                    State.Bots.Add(bot);

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
                        .LogAndForget(Logger, "send a new avatar to room {RoomId}", State.RoomId);
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
                        await GrainFactory.SendComposerToPlayerAsync(playerId, composer, ct);
                }
                else if (announce)
                    await _roomGrain.SendComposerToRoomAsync(
                        item.GetRemoveComposer(pickerId, delay: removeDelayMs),
                        ct
                    );

                await item.Logic.OnDetachAsync(ct);
                await item.Logic.OnPickupAsync(ctx, ct);

                item.SetAction(null);

                State.ItemsById.Remove(item.ObjectId);
                State.ItemsVersion++;

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

                State.AvatarsByObjectId.Remove(avatar.ObjectId);
                AvatarModule.RememberDeparted(avatar);

                if (avatar is IRoomPet pet)
                    State.Pets.Remove(pet);
                else if (avatar is IRoomBot bot)
                    State.Bots.Remove(bot);
                break;
            }
        }

        return true;
    }

    // The logic provider's version the furni's logic was last checked against.
    private int _logicVersion = -1;

    /// <summary>
    /// After a logic type was registered or taken away (a plugin loading, reloading or
    /// unloading), gives each furni whose logic is no longer what its type makes
    /// (<see cref="IRoomObjectLogicProvider.IsCurrent"/>) a new one: the old one is detached (a
    /// failure, as from a reloaded plugin's disposed services, is logged), and the new one made
    /// and attached as the room's load does. A no-op until the provider's version moves.
    /// </summary>
    internal async Task RebindStaleLogicsAsync(CancellationToken ct)
    {
        var provider = _roomGrain._logicProvider;
        var version = provider.Version;

        if (version == _logicVersion)
            return;

        _logicVersion = version;

        foreach (var item in State.ItemsById.Values.ToList())
        {
            if (item.Logic is null || provider.IsCurrent(item.Logic))
                continue;

            try
            {
                await item.Logic.OnDetachAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogWarning(
                    ex,
                    "The old logic of item {ItemId} in room {RoomId} failed to detach",
                    item.ObjectId,
                    State.RoomId
                );
            }

            item.SetLogic(null!);

            await AttatchLogicAsync(item, ct);
        }
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
