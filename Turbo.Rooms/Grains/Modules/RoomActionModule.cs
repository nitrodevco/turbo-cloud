using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Quests.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomActionModule(RoomGrain roomGrain) : RoomGrainComponent(roomGrain)
{
    public Task<bool> AddItemAsync(IRoomItem item, CancellationToken ct) =>
        ObjectModule.AttatchObjectAsync(item, ct);

    /// <param name="cause">Why it leaves the room; written with its movement into the inventory.</param>
    public async Task<bool> RemoveItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        bool confirmed,
        MovementCause cause,
        CancellationToken ct
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        // A variable box takes what it stored with it, so only the room's owner and its own may
        // pick it up; anyone else is told (a room admin who could pick up borrowed furni was
        // refused 8 variables with furni_placement_error room.error.variable_permissions).
        if (
            item.Logic is FurnitureWiredVariableLogic
            && !await SecurityModule.CanPickUpVariableAsync(ctx, item)
        )
        {
            await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                ctx.PlayerId,
                new NotificationDialogMessageComposer
                {
                    NotificationType = FurniturePlacementNotifications.PLACEMENT_ERROR,
                    Parameters = ImmutableDictionary<string, string>.Empty.Add(
                        "message",
                        FurniturePlacementNotifications.VARIABLE_PERMISSIONS_MESSAGE
                    ),
                },
                ct
            );

            return false;
        }

        // A borrowed furni is given back rather than picked up: it belongs to the club, so it
        // reaches no inventory and the room simply lets go of it. The client warns the player
        // that it cannot be borrowed again before it asks for this.
        if (item.IsBuildersClub)
        {
            if (await SecurityModule.GetControllerLevelAsync(ctx) < RoomControllerType.GroupAdmin)
                throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

            if (!confirmed && await AskToConfirmPickupAsync(ctx, item, ct))
                return false;

            if (!await ObjectModule.RemoveObjectAsync(ctx, item, ct, ctx.PlayerId))
                return false;

            await _roomGrain
                ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
                .EnqueueGiveBackAsync(_roomGrain.RoomId, [item.GetSnapshot()], cause, ct);

            return true;
        }

        var pickupType = await SecurityModule.GetFurniPickupTypeAsync(ctx);

        // Whatever a player may do in the room, their own furni is theirs to take back: someone
        // who built on a rented space, or whose rights were taken away, is not stuck with it here.
        if (pickupType == FurniturePickupType.None && item.OwnerId == ctx.PlayerId)
            pickupType = FurniturePickupType.SendToOwner;

        if (pickupType == FurniturePickupType.None)
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        // A temporary furni is picked up like any other and then is simply gone: it reaches no
        // inventory (official client capture, WIN63-202610131318: ObjectRemove and a height map
        // update, no inventory update).
        if (item.IsTemporary)
            return await ObjectModule.RemoveObjectAsync(ctx, item, ct, ctx.PlayerId);

        if (!confirmed && await AskToConfirmPickupAsync(ctx, item, ct))
            return false;

        PlayerId pickerId = item.OwnerId;

        if (pickupType is FurniturePickupType.SendToCtx)
            pickerId = ctx.PlayerId;

        item.SetOwnerId(pickerId);

        // Habbo tells the room, then the heights, then the owner's inventory (official client
        // captures 2026-10-13 21:38 and 2026-10-16 09:03-09:04: ObjectRemove, HeightMapUpdate,
        // FurniListAddOrUpdate 5/5 floor; ItemRemove, FurniListAddOrUpdate 2/2 wall). An owner
        // standing here hears of the removal first, so their inventory's update follows it.
        var ownerHere =
            pickerId == ctx.PlayerId || AvatarModule.GetPlayerPosition(pickerId) is not null;

        await ObjectModule.RemoveObjectAsync(
            ctx,
            item,
            ct,
            pickerId,
            toldFirst: ownerHere ? pickerId : null
        );

        var snapshot = item.GetSnapshot();

        // The row is the inventory's before the inventory lists it, so whatever it does next
        // with the item (a trade, a sale) finds it there.
        await _roomGrain
            ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
            .WritePickupAsync(_roomGrain.RoomId, [snapshot], cause, ct);

        var inventory = _roomGrain._grainFactory.GetInventoryGrain(snapshot.OwnerId);

        await inventory.AddFurnitureFromRoomItemSnapshotAsync(snapshot, ct);

        return true;
    }

    /// <summary>
    /// Asks the player to confirm picking up an item that loses something on the way out, after
    /// the room has decided they may. True when they were asked, and the pickup waits for the
    /// client to send it again confirmed.
    /// </summary>
    private async Task<bool> AskToConfirmPickupAsync(
        ActionContext ctx,
        IRoomItem item,
        CancellationToken ct
    )
    {
        if (item.Logic.GetPickupConfirmation() is not { } confirmation)
            return false;

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new ObjectRemoveConfirmMessageComposer
            {
                Category =
                    item is IRoomFloorItem ? RoomItemCategoryType.Floor : RoomItemCategoryType.Wall,
                ObjectId = item.ObjectId,
                Confirmation = confirmation,
            },
            ct
        );

        return true;
    }

    /// <summary>
    /// <c>:pickall</c> and <c>:pickallbc</c>: every furni the player owns in the room goes back
    /// to them at once, their own furni to their inventory or, for <c>:pickallbc</c>, their
    /// borrowed furni to the club. Anybody's own furni is theirs to take back, as one at a time.
    /// Returns how many left the room.
    /// </summary>
    public async Task<int> PickUpAllOwnedAsync(
        ActionContext ctx,
        bool buildersClub,
        CancellationToken ct
    )
    {
        var items = _roomGrain
            ._state.ItemsById.Values.Where(x =>
                x.OwnerId == ctx.PlayerId && !x.IsTemporary && x.IsBuildersClub == buildersClub
            )
            .ToList();

        await ReturnItemsToOwnersAsync(
            items,
            MovementCause.New(MovementCauses.ROOM_PICKALL, ctx.PlayerId),
            ct
        );

        return items.Count;
    }

    /// <summary>
    /// <c>:ejectall</c>: everybody else's furni goes back to its owners, for whoever may eject
    /// one furni at a time. The room owner's furni and the player's own stay, and so do other
    /// players' variable boxes unless the player is the room's owner, as one at a time. Null when the
    /// player may not eject here; otherwise how many left the room.
    /// </summary>
    public async Task<int?> EjectAllAsync(ActionContext ctx, CancellationToken ct)
    {
        if (await SecurityModule.GetFurniPickupTypeAsync(ctx) == FurniturePickupType.None)
            return null;

        var roomOwnerId = _roomGrain._state.RoomSnapshot.OwnerId;
        var mayTakeVariables = await SecurityModule.GetIsRoomOwnerAsync(ctx);
        var items = _roomGrain
            ._state.ItemsById.Values.Where(x =>
                x.OwnerId != ctx.PlayerId
                && x.OwnerId != roomOwnerId
                && !x.IsTemporary
                && (mayTakeVariables || x.Logic is not FurnitureWiredVariableLogic)
            )
            .ToList();

        await ReturnItemsToOwnersAsync(
            items,
            MovementCause.New(MovementCauses.ROOM_EJECTALL, ctx.PlayerId),
            ct
        );

        return items.Count;
    }

    /// <summary>
    /// Sends many items home at once: the room lets go of all of them first, then each owner's
    /// inventory takes its share in one call, owners side by side. The room is told once per
    /// owner, floor items in one ObjectRemoveMultiple and wall items in one ItemRemoveMultiple
    /// (the client removes each list together), borrowed and temporary furni included. The rows
    /// go to their owners in one write under <paramref name="cause"/>, before any inventory lists them.
    /// </summary>
    public async Task ReturnItemsToOwnersAsync(
        IReadOnlyCollection<IRoomItem> items,
        MovementCause cause,
        CancellationToken ct
    )
    {
        if (items.Count == 0)
            return;

        var ctx = ActionContext.CreateForSystem(_roomGrain.RoomId);
        var returned = new Dictionary<PlayerId, List<IRoomItem>>();
        var removed = new Dictionary<PlayerId, List<IRoomItem>>();
        var borrowsGivenBack = new Dictionary<PlayerId, List<string>>();
        var borrowed = new List<RoomItemSnapshot>();

        foreach (var item in items)
        {
            if (
                !await ObjectModule.RemoveObjectAsync(
                    ctx,
                    item,
                    ct,
                    item.OwnerId,
                    announce: false,
                    reportBorrow: false
                )
            )
                continue;

            if (!removed.TryGetValue(item.OwnerId, out var gone))
                removed[item.OwnerId] = gone = [];

            gone.Add(item);

            // A borrowed furni goes back to the club rather than to anybody's inventory. They
            // are gathered up here so each borrower is told their new total once.
            if (item.IsBuildersClub)
            {
                if (!borrowsGivenBack.TryGetValue(item.OwnerId, out var logicNames))
                    borrowsGivenBack[item.OwnerId] = logicNames = [];

                logicNames.Add(item.Definition.LogicName);
                borrowed.Add(item.GetSnapshot());

                continue;
            }

            // A temporary furni leaves the room like any other and then is simply gone.
            if (item.IsTemporary)
                continue;

            if (!returned.TryGetValue(item.OwnerId, out var owned))
                returned[item.OwnerId] = owned = [];

            owned.Add(item);
        }

        foreach (var (ownerId, gone) in removed)
        {
            var floorIds = gone.Where(x => x is IRoomFloorItem)
                .Select(x => (long)x.ObjectId.Value)
                .ToList();
            var wallIds = gone.Where(x => x is IRoomWallItem)
                .Select(x => (long)x.ObjectId.Value)
                .ToList();

            if (floorIds.Count > 0)
                await _roomGrain.SendComposerToRoomAsync(
                    new ObjectRemoveMultipleMessageComposer
                    {
                        ObjectIdsToRemove = [.. floorIds],
                        PickerId = ownerId,
                        ByInvisibleFurniManager = false,
                    },
                    ct
                );

            if (wallIds.Count > 0)
                await _roomGrain.SendComposerToRoomAsync(
                    new ItemRemoveMultipleMessageComposer
                    {
                        ObjectIdsToRemove = [.. wallIds],
                        PickerId = ownerId,
                        ByInvisibleFurniManager = false,
                    },
                    ct
                );
        }

        // The borrowed ones are numbered after the furni that go home, under the same cause.
        if (borrowed.Count > 0)
            await _roomGrain
                ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
                .EnqueueGiveBackAsync(
                    _roomGrain.RoomId,
                    borrowed,
                    cause.StartingAt(cause.FirstSeq + returned.Values.Sum(x => x.Count)),
                    ct
                );

        var buildersClub = _roomGrain._grainFactory.GetBuildersClubGrain();

        foreach (var (playerId, logicNames) in borrowsGivenBack)
            await buildersClub.OnReturnedAsync(playerId, [.. logicNames], ct);

        if (returned.Count == 0)
            return;

        await _roomGrain
            ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
            .WritePickupAsync(
                _roomGrain.RoomId,
                [.. returned.Values.SelectMany(x => x).Select(x => x.GetSnapshot())],
                cause,
                ct
            );

        await Task.WhenAll(
            returned.Select(entry =>
                _roomGrain
                    ._grainFactory.GetInventoryGrain(entry.Key)
                    .AddFurnitureFromRoomItemSnapshotsAsync(
                        [.. entry.Value.Select(x => x.GetSnapshot())],
                        ct
                    )
            )
        );
    }

    public async Task<bool> UseItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        // A find task completes on the double-click itself ("find the BBQ and double-click on
        // it", quests.daily.FINDBBQ.hint), whether or not the player may change the furni.
        if (ctx.Origin == ActionOrigin.Player && ctx.PlayerId > 0)
            _roomGrain
                ._grainFactory.GetPlayerDailyTaskGrain(ctx.PlayerId)
                .RecordActivityAsync(
                    DailyTaskActivity.FurniUse,
                    item.Definition.Name,
                    CancellationToken.None
                )
                .LogAndForget(
                    _roomGrain._logger,
                    "count the use of item {ItemId} by player {PlayerId} for daily tasks",
                    itemId,
                    ctx.PlayerId
                );

        if (!await item.Logic.CanUseAsync(ctx))
            return false;

        var previousState = item.Logic.GetState();
        await item.Logic.OnUseAsync(ctx, param, ct);

        if (!item.Logic.ReportsOwnUse())
            await ReportItemUsedAsync(
                ctx,
                item.ObjectId,
                previousState != item.Logic.GetState(),
                ct
            );

        return true;
    }

    /// <summary>
    /// What a use of the item counts for once it has happened: the furniture toggle achievement
    /// when it changed the item's state, and the room's used event ("Furni Is Used" triggers).
    /// </summary>
    public async Task ReportItemUsedAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        bool stateChanged,
        CancellationToken ct
    )
    {
        // A rejected gate use and an action with no state change are not qualifying toggles.
        if (ctx.Origin == ActionOrigin.Player && stateChanged)
        {
            await using var db = await _roomGrain._dbCtxFactory.CreateDbContextAsync(ct);
            _roomGrain._achievementFacts.Record(
                db,
                ctx.PlayerId,
                new()
                {
                    Source = AchievementSources.FURNITURE,
                    OperationId = Guid.NewGuid().ToString("N"),
                    OccurredAtUtc = DateTime.UtcNow,
                }
            );
            await db.SaveChangesAsync(ct);
        }

        await _roomGrain.PublishRoomEventAsync(
            new RoomItemUsedEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ctx,
                ObjectId = itemId,
            },
            ct
        );
    }

    /// <summary>
    /// Dedicated furniture actions carry their own permission rules (owner-only edits, rights,
    /// adjacency), so the logic checks them rather than the item usage policy.
    /// </summary>
    public async Task<bool> InteractWithItemAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        return await item.Logic.OnInteractAsync(ctx, interaction, ct);
    }

    /// <summary>
    /// A music action for the room's jukebox or sound machine, the first one placed when there
    /// are several; the client plays one per room. False when the room has none.
    /// </summary>
    public async Task<bool> InteractWithMusicPlayerAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        var player = _roomGrain
            ._state.ItemsById.Values.Where(x => x.Logic is FurnitureJukeboxLogic)
            .MinBy(x => x.ObjectId.Value);

        if (player is null)
            return false;

        return await player.Logic.OnInteractAsync(ctx, interaction, ct);
    }

    public async Task<bool> ClickItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        // The client can click its placement preview before PlaceObject arrives, or an item
        // another player has just picked up. Neither click targets a live room item yet.
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            return false;

        await item.Logic.OnClickAsync(ctx, param, ct);

        return true;
    }

    public async Task<bool> SetItemDataAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        string color,
        string text,
        CancellationToken ct
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        if (!await CanEditItemAsync(ctx, item))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        var config = _roomGrain._roomConfig;

        if (!StickieColors.IsValid(color) || text.Length > config.StickieTextMaxLength)
        {
            _roomGrain._logger.LogWarning(
                "Rejected item data on {ItemId} in room {RoomId} from player {PlayerId}: colour {Color}, {Length} characters",
                itemId,
                _roomGrain.RoomId,
                ctx.PlayerId,
                color,
                text.Length
            );

            return false;
        }

        text = _roomGrain._wordFilter.FilterAndTruncate(text, config.StickieTextMaxLength);

        await item.Logic.SetLegacyDataAsync(StickieColors.Compose(color.ToUpperInvariant(), text));

        return true;
    }

    private async Task<bool> CanSetObjectDataAsync(ActionContext ctx) =>
        ctx.Origin == ActionOrigin.System
        || await SecurityModule.HasPermissionAsync(
            ctx.PlayerId,
            PermissionNodes.Room.FURNI_BRANDING
        )
        || await SecurityModule.HasPermissionAsync(
            ctx.PlayerId,
            PermissionNodes.Room.FURNI_CUSTOM_VARIABLES
        )
        || await SecurityModule.HasPermissionAsync(
            ctx.PlayerId,
            PermissionNodes.Room.FURNI_VIMEO_EDIT
        );

    public async Task<bool> SetObjectDataAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        IReadOnlyDictionary<string, string> entries,
        CancellationToken ct
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        // Every client path that sends object data is a staff one: saving an ad furni's branding
        // (security level 4), the info stand's custom variables and a Vimeo display's video (both
        // level 5). Room rights never reached it, so they do not here either.
        if (!await CanSetObjectDataAsync(ctx))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        var config = _roomGrain._roomConfig;

        if (
            entries.Count > config.ObjectDataMaxEntries
            || entries.Any(x =>
                string.IsNullOrEmpty(x.Key)
                || x.Key.Length > config.ObjectDataMaxKeyLength
                || x.Value.Length > config.ObjectDataMaxValueLength
            )
        )
        {
            _roomGrain._logger.LogWarning(
                "Rejected object data on {ItemId} in room {RoomId} from player {PlayerId}: {Count} entries",
                itemId,
                _roomGrain.RoomId,
                ctx.PlayerId,
                entries.Count
            );

            return false;
        }

        return await item.Logic.SetMapDataAsync(entries);
    }

    /// <param name="cause">What used it up; written with its movement out of existence.</param>
    /// <param name="removeDelayMs">How long the client waits before it takes the furni away.</param>
    /// <param name="byOwner">
    /// False when nobody picks it up, so the client draws no pickup towards its owner.
    /// </param>
    public async Task<bool> DeleteItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        MovementCause cause,
        CancellationToken ct,
        int removeDelayMs = 0,
        bool byOwner = true
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (!await CanEditItemAsync(ctx, item))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        await ObjectModule.RemoveObjectAsync(
            ctx,
            item,
            ct,
            byOwner ? item.OwnerId : -1,
            removeDelayMs: removeDelayMs
        );

        // Written before the caller grants what the item was worth, and with the pets it changed
        // (a saddle strapped on): queued for the flush, the row outlived the grant by up to
        // RoomConfig.DirtyItemsTickMs, and a crash in between gave the item back.
        await _roomGrain
            ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
            .WriteUseAsync(
                _roomGrain.RoomId,
                new UsedUpItem(item.GetSnapshot(), cause),
                PetModule.TakeDirtySnapshots(),
                ct
            );

        return true;
    }

    /// <summary>
    /// Puts an item used up for a grant that was then refused (the owner holds the most pets
    /// allowed) back in its owner's inventory. The item goes first so a crash cannot leave both;
    /// a borrowed or temporary item has nowhere to go and is only logged.
    /// </summary>
    internal async Task GiveBackUsedItemAsync(IRoomItem item, CancellationToken ct)
    {
        if (item.IsBuildersClub || item.IsTemporary)
        {
            _roomGrain._logger.LogWarning(
                "Used item {ItemId} in room {RoomId} is borrowed or temporary; its refused grant is not given back",
                item.ObjectId,
                _roomGrain.RoomId
            );

            return;
        }

        await _roomGrain
            ._grainFactory.GetInventoryGrain(item.OwnerId)
            .GrantFurnitureAsync(
                item.Definition.Id,
                item.ExtraData.GetJsonString(),
                MovementCause.New(
                    MovementCauses.ROOM_GIVE_BACK_USED,
                    item.OwnerId.Value,
                    item.ObjectId.Value
                ),
                ct
            );
    }

    /// <summary>The item's owner may always edit it; otherwise room rights are needed.</summary>
    private async Task<bool> CanEditItemAsync(ActionContext ctx, IRoomItem item) =>
        item.OwnerId == ctx.PlayerId || await SecurityModule.CanManipulateFurniAsync(ctx);
}
