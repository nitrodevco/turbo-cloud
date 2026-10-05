using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Grains.Messenger;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms;

internal sealed partial class RoomService(
    ILogger<IRoomService> logger,
    IOptions<RoomConfig> roomConfig,
    IGrainFactory grainFactory
) : IRoomService
{
    private readonly ILogger<IRoomService> _logger = logger;
    private readonly RoomConfig _roomConfig = roomConfig.Value;
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async Task<FollowFriendErrorCodeType?> FollowFriendAsync(
        PlayerId playerId,
        PlayerId friendId,
        CancellationToken ct
    )
    {
        if (playerId <= 0 || friendId <= 0)
            return FollowFriendErrorCodeType.NotFriend;

        if (
            !await _grainFactory
                .GetPlayerMessengerGrain(playerId)
                .IsFriendAsync(friendId, ct)
                .ConfigureAwait(false)
        )
            return FollowFriendErrorCodeType.NotFriend;

        var friendPresence = _grainFactory.GetPlayerPresenceGrain(friendId);

        if (!await friendPresence.HasActiveSessionAsync(ct).ConfigureAwait(false))
            return FollowFriendErrorCodeType.Offline;

        var activeRoom = await friendPresence.GetActiveRoomAsync(ct).ConfigureAwait(false);

        if (activeRoom.RoomId <= 0)
            return FollowFriendErrorCodeType.HotelView;

        var access = await CheckRoomEntryAccessAsync(
                playerId,
                activeRoom.RoomId,
                password: null,
                bypassDoor: false,
                ct
            )
            .ConfigureAwait(false);

        if (access != RoomEntryAccessType.Allowed)
            return FollowFriendErrorCodeType.Prevented;

        await _grainFactory
            .ForwardPlayerToRoomAsync(playerId, activeRoom.RoomId, ct)
            .ConfigureAwait(false);

        return null;
    }

    public async Task<RoomEntryAccessType> CheckRoomEntryAccessAsync(
        PlayerId playerId,
        RoomId roomId,
        string? password,
        bool bypassDoor,
        CancellationToken ct
    )
    {
        var room = _grainFactory.GetRoomGrain(roomId);

        var access = await RoomTelemetry
            .MeasureAsync(
                RoomTelemetry.ACCESS,
                roomId,
                () => room.CheckEntryAccessAsync(playerId, password, bypassDoor, ct)
            )
            .ConfigureAwait(false);

        // Activation already hydrates metadata, rights and bans. Rejected visitors need none
        // of the map, furniture or NPC state. Keep allowed loading before the caller clears
        // the player's current room, so a load failure leaves that session intact.
        if (access == RoomEntryAccessType.Allowed)
        {
            await RoomTelemetry
                .MeasureAsync(RoomTelemetry.ACTIVATE, roomId, () => room.EnsureRoomActiveAsync(ct))
                .ConfigureAwait(false);

            // A cold load can yield long enough for bans, capacity or deletion to change.
            access = await RoomTelemetry
                .MeasureAsync(
                    RoomTelemetry.ACCESS,
                    roomId,
                    () => room.CheckEntryAccessAsync(playerId, password, bypassDoor, ct)
                )
                .ConfigureAwait(false);
        }

        return access;
    }

    /// <summary>
    /// OpenConnection is sent first so the client leaves the hotel view, then the decision is
    /// applied: banned/full → CantConnect + CloseConnection, wrong or missing password →
    /// GenericError + CloseConnection, locked → doorbell ring (FlatAccessDenied when nobody can
    /// answer), allowed → the room is streamed.
    /// </summary>
    public async Task OpenRoomForPlayerIdAsync(
        ActionContext ctx,
        PlayerId playerId,
        RoomId roomId,
        RoomEntryAccessType access,
        CancellationToken ct
    )
    {
        if (playerId <= 0 || roomId <= 0)
            return;

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(playerId);

        // Re-entering the room the player is already in is a full reload, not a no-op: by the time
        // this arrives the client has already torn its room view down and is waiting for the entry
        // sequence. Returning early here leaves it on a black screen forever.
        await RoomTelemetry
            .MeasureAsync(
                RoomTelemetry.PREPARE_PLAYER,
                roomId,
                async () =>
                {
                    await LeavePendingDoorbellAsync(playerPresence, playerId, roomId, ct)
                        .ConfigureAwait(false);
                    await playerPresence.ClearActiveRoomAsync(ct).ConfigureAwait(false);
                }
            )
            .ConfigureAwait(false);

        await _grainFactory
            .SendComposerToPlayerAsync(
                playerId,
                new OpenConnectionMessageComposer { RoomId = roomId },
                ct
            )
            .ConfigureAwait(false);

        var room = _grainFactory.GetRoomGrain(roomId);

        switch (access)
        {
            case RoomEntryAccessType.Banned:
                await RejectEntryAsync(
                        playerPresence,
                        new CantConnectMessageComposer
                        {
                            ErrorType = RoomConnectionErrorType.Banned,
                            AdditionalInfo = string.Empty,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.Full:
                await RejectEntryAsync(
                        playerPresence,
                        new CantConnectMessageComposer
                        {
                            ErrorType = RoomConnectionErrorType.RoomFull,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.PasswordRequired:
            case RoomEntryAccessType.InvalidPassword:
                await RejectEntryAsync(
                        playerPresence,
                        new GenericErrorMessage
                        {
                            ErrorCode = RoomGenericErrorType.InvalidPassword,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.HiddenByBuildersClub:
                // The pop-up says what the generic refusal cannot: the room is not gone, its
                // owner has let a Builders Club membership lapse.
                await RejectEntryAsync(
                        playerPresence,
                        new CantConnectMessageComposer
                        {
                            ErrorType = RoomConnectionErrorType.NoEntry,
                        },
                        ct,
                        new NotificationDialogMessageComposer
                        {
                            NotificationType = BuildersClubNotifications.VISIT_DENIED_FOR_VISITOR,
                        }
                    )
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.Closed:
                await RejectEntryAsync(
                        playerPresence,
                        new CantConnectMessageComposer
                        {
                            ErrorType = RoomConnectionErrorType.NoEntry,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.Doorbell:
                await RingDoorbellAsync(playerPresence, playerId, room, roomId, ct)
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.Allowed:
                await playerPresence
                    .SetPendingRoomAsync(roomId, RoomEntryState.Approved, ct)
                    .ConfigureAwait(false);
                await EnterRoomAsync(ctx, playerPresence, room, roomId, ct).ConfigureAwait(false);
                return;

            default:
                _logger.LogWarning(
                    "Unhandled entry access {Access} for player {PlayerId} in room {RoomId}",
                    access,
                    playerId,
                    roomId
                );
                await RejectEntryAsync(
                        playerPresence,
                        new CantConnectMessageComposer
                        {
                            ErrorType = RoomConnectionErrorType.NoEntry,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                return;
        }
    }

    public async Task<bool> KickPlayerAsync(
        ActionContext ctx,
        PlayerId targetId,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || targetId <= 0)
            return false;

        var room = _grainFactory.GetRoomGrain(ctx.RoomId);

        if (!await room.KickPlayerAsync(ctx, targetId, ct).ConfigureAwait(false))
            return false;

        await EvictPlayerAsync(ctx.RoomId, targetId, kicked: true, ct).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> BanPlayerAsync(
        ActionContext ctx,
        PlayerId targetId,
        RoomBanDurationType duration,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || targetId <= 0)
            return false;

        var room = _grainFactory.GetRoomGrain(ctx.RoomId);

        if (!await room.BanPlayerAsync(ctx, targetId, duration, ct).ConfigureAwait(false))
            return false;

        await EvictPlayerAsync(ctx.RoomId, targetId, kicked: true, ct).ConfigureAwait(false);

        return true;
    }

    public async Task DeleteRoomAsync(ActionContext ctx, CancellationToken ct)
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var room = _grainFactory.GetRoomGrain(ctx.RoomId);
        var players = await room.PrepareRoomDeletionAsync(ctx, ct).ConfigureAwait(false);

        if (players is null)
            return;

        // Sessions close before the row goes, so no presence grain re-enters a room mid-delete.
        foreach (var playerId in players.Value)
            await EvictPlayerAsync(ctx.RoomId, playerId, kicked: false, ct).ConfigureAwait(false);

        await room.CompleteRoomDeletionAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes a player's room session after the room grain has already dropped their avatar.
    /// The presence does it in one call that never re-enters the room; wired kicks use the
    /// same call from inside the room grain.
    /// </summary>
    private Task EvictPlayerAsync(
        RoomId roomId,
        PlayerId playerId,
        bool kicked,
        CancellationToken ct
    ) => _grainFactory.GetPlayerPresenceGrain(playerId).OnRemovedFromRoomAsync(roomId, kicked, ct);

    public async Task AnswerDoorbellAsync(
        ActionContext ctx,
        string playerName,
        bool accepted,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || string.IsNullOrWhiteSpace(playerName))
            return;

        var room = _grainFactory.GetRoomGrain(ctx.RoomId);
        var ringerId = await room.AnswerDoorbellAsync(ctx, playerName, accepted, ct)
            .ConfigureAwait(false);

        if (ringerId is null)
            return;

        var ringerPresence = _grainFactory.GetPlayerPresenceGrain(ringerId.Value);
        var pendingRoom = await ringerPresence.GetPendingRoomAsync(ct).ConfigureAwait(false);

        // The ringer moved on (entered elsewhere, quit, or disconnected) while waiting.
        if (
            pendingRoom.RoomId != ctx.RoomId
            || pendingRoom.State != RoomEntryState.RingingDoorbell
            || !await ringerPresence.HasActiveSessionAsync(ct).ConfigureAwait(false)
        )
        {
            await ringerPresence.ClearPendingRoomAsync(ct).ConfigureAwait(false);

            return;
        }

        if (!accepted)
        {
            await ringerPresence.ClearPendingRoomAsync(ct).ConfigureAwait(false);
            await _grainFactory
                .SendComposerToPlayerAsync(
                    ringerId.Value,
                    new FlatAccessDeniedMessageComposer
                    {
                        RoomId = ctx.RoomId,
                        Username = string.Empty,
                    },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        await RoomTelemetry
            .MeasureAsync(RoomTelemetry.ACTIVATE, ctx.RoomId, () => room.EnsureRoomActiveAsync(ct))
            .ConfigureAwait(false);

        await ringerPresence
            .SetPendingRoomAsync(ctx.RoomId, RoomEntryState.Approved, ct)
            .ConfigureAwait(false);

        await EnterRoomAsync(
                ActionContext.CreateForPlayer(ringerId.Value, ctx.RoomId),
                ringerPresence,
                room,
                ctx.RoomId,
                ct
            )
            .ConfigureAwait(false);
    }

    public async Task CloseRoomForPlayerAsync(PlayerId playerId, CancellationToken ct)
    {
        if (playerId <= 0)
            return;

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(playerId);

        await LeavePendingDoorbellAsync(playerPresence, playerId, RoomId.Invalid, ct)
            .ConfigureAwait(false);
        await playerPresence.ClearActiveRoomAsync(ct).ConfigureAwait(false);

        await _grainFactory
            .SendComposerToPlayerAsync(playerId, new CloseConnectionMessageComposer(), ct)
            .ConfigureAwait(false);
    }

    private async Task RingDoorbellAsync(
        IPlayerPresenceGrain playerPresence,
        PlayerId playerId,
        IRoomGrain room,
        RoomId roomId,
        CancellationToken ct
    )
    {
        var player = await _grainFactory
            .GetPlayerGrain(playerId)
            .GetSummaryAsync(ct)
            .ConfigureAwait(false);

        await playerPresence
            .SetPendingRoomAsync(roomId, RoomEntryState.RingingDoorbell, ct)
            .ConfigureAwait(false);

        var notified = await room.RingDoorbellAsync(playerId, player.Name, ct)
            .ConfigureAwait(false);

        if (notified)
            return;

        await playerPresence.ClearPendingRoomAsync(ct).ConfigureAwait(false);
        await _grainFactory
            .SendComposerToPlayerAsync(
                playerId,
                new FlatAccessDeniedMessageComposer { RoomId = roomId, Username = string.Empty },
                ct
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the player from the doorbell queue of any room they are still waiting on, unless
    /// that room is the one they are entering now.
    /// </summary>
    private async Task LeavePendingDoorbellAsync(
        IPlayerPresenceGrain playerPresence,
        PlayerId playerId,
        RoomId exceptRoomId,
        CancellationToken ct
    )
    {
        var pendingRoom = await playerPresence.GetPendingRoomAsync(ct).ConfigureAwait(false);

        if (pendingRoom.RoomId <= 0 || pendingRoom.RoomId == exceptRoomId)
            return;

        if (pendingRoom.State == RoomEntryState.RingingDoorbell)
            await _grainFactory
                .GetRoomGrain(pendingRoom.RoomId)
                .RemoveDoorbellRingerAsync(playerId, ct)
                .ConfigureAwait(false);

        await playerPresence.ClearPendingRoomAsync(ct).ConfigureAwait(false);
    }

    /// <param name="notice">Shown before the refusal, in the same batch, when the refusal needs explaining.</param>
    private static async Task RejectEntryAsync(
        IPlayerPresenceGrain playerPresence,
        IComposer error,
        CancellationToken ct,
        IComposer? notice = null
    )
    {
        await playerPresence.ClearPendingRoomAsync(ct).ConfigureAwait(false);
        await playerPresence
            .SendComposerAsync(
                notice is null
                    ? [error, new CloseConnectionMessageComposer()]
                    : [notice, error, new CloseConnectionMessageComposer()],
                ct
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Streams the room to a player whose entry has already been approved and whose client has
    /// already received OpenConnection.
    /// </summary>
    private async Task EnterRoomAsync(
        ActionContext ctx,
        IPlayerPresenceGrain playerPresence,
        IRoomGrain room,
        RoomId roomId,
        CancellationToken ct
    )
    {
        var roomCtx = ctx with { RoomId = roomId };

        // One call for everything the room shows, taken in one of its turns.
        var view = await RoomTelemetry
            .MeasureAsync(
                RoomTelemetry.ENTRY_VIEW,
                roomId,
                () => room.GetEntryViewAsync(ctx.PlayerId, ct)
            )
            .ConfigureAwait(false);
        var snapshot = view.Room;
        var mapSnapshot = view.Map;

        // The client's initial camera target is the door tile.
        var doorTileIndex = mapSnapshot.DoorY * mapSnapshot.Width + mapSnapshot.DoorX;
        var doorAltitude =
            doorTileIndex >= 0 && doorTileIndex < mapSnapshot.TileEncodedHeights.Length
                ? Altitude.FromValue(
                    mapSnapshot.TileEncodedHeights[doorTileIndex] < 0
                        ? 0
                        : (mapSnapshot.TileEncodedHeights[doorTileIndex] & 0x3FFF) / 256.0
                )
                : Altitude.Zero;

        // The whole entry sequence as one batch to the player, in the order the client expects
        // it; it used to go out in eleven sends with a grain call between some of them.
        var composers = new List<IComposer>
        {
            new RoomReadyMessageComposer { WorldType = snapshot.WorldType, RoomId = roomId },
            new RoomRatingMessageComposer { Rating = snapshot.Score, CanRate = view.CanRate },
            new RoomEntryTileMessageComposer
            {
                X = mapSnapshot.DoorX,
                Y = mapSnapshot.DoorY,
                Rotation = mapSnapshot.DoorRotation,
            },
            new HeightMapMessageComposer
            {
                Width = mapSnapshot.Width,
                Size = mapSnapshot.Size,
                Heights = mapSnapshot.TileEncodedHeights,
            },
            new FloorHeightMapMessageComposer
            {
                ScaleType = _roomConfig.DefaultRoomScale,
                // The room's own choice, made in the floor plan editor; a room that has
                // never been drawn keeps the hotel's default.
                FixedWallsHeight =
                    snapshot.WallHeight >= 0 ? snapshot.WallHeight : _roomConfig.DefaultWallHeight,
                ModelData = mapSnapshot.ModelData,
                AreaHideData = [],
                CameraInitX = mapSnapshot.DoorX,
                CameraInitY = mapSnapshot.DoorY,
                CameraInitZ = doorAltitude,
            },
            new RoomVisualizationSettingsMessageComposer
            {
                WallsHidden = snapshot.HideWalls,
                WallThickness = snapshot.WallThickness,
                FloorThickness = snapshot.FloorThickness,
            },
            new RoomChatSettingsMessageComposer { ChatProtection = snapshot.ChatProtection },
        };

        if (view.ActiveEvent is not null)
            composers.Add(
                new RoomEventMessageComposer
                {
                    Event = view.ActiveEvent,
                    SentAtUtc = DateTime.UtcNow,
                }
            );

        if (view.IsMuted)
            composers.Add(new MuteAllInRoomEventMessageComposer { IsMuted = true });

        composers.AddRange(
            view.Properties.Select(x => new RoomPropertyMessageComposer
            {
                Key = RoomPropertyTypeExtensions.GetString(x.Key),
                Value = x.Value,
            })
        );

        composers.Add(
            new ObjectsMessageComposer
            {
                OwnerNames = view.OwnerNames,
                FloorItems = view.FloorItems,
            }
        );
        composers.Add(
            new ItemsMessageComposer { OwnerNames = view.OwnerNames, WallItems = view.WallItems }
        );
        composers.Add(new UsersMessageComposer { Avatars = view.Avatars });
        composers.Add(new UserUpdateMessageComposer { Avatars = view.Avatars });

        // Neither a dance, an effect nor a sleep travels in the Users packet, so all three are
        // replayed to the arriving player as their own updates. Any avatar can carry them; one
        // that the client does not draw as a user simply never has one to replay.
        composers.AddRange(
            view.Avatars.Where(x => x.DanceType != AvatarDanceType.None)
                .Select(x => new DanceMessageComposer
                {
                    ObjectId = x.ObjectId,
                    DanceType = x.DanceType,
                })
        );
        composers.AddRange(
            view.Avatars.Where(x => x.EffectId > 0)
                .Select(x => new AvatarEffectMessageComposer
                {
                    ObjectId = x.ObjectId,
                    EffectId = x.EffectId,
                    DelayMilliseconds = 0,
                })
        );
        composers.AddRange(
            view.Avatars.Where(x => x.IsIdle)
                .Select(x => new SleepMessageComposer { ObjectId = x.ObjectId, IsSleeping = true })
        );

        // A batch for one player is the one direct presence send AGENTS.md allows.
        await RoomTelemetry
            .MeasureAsync(
                RoomTelemetry.INITIAL_PACKETS,
                roomId,
                () => playerPresence.SendComposerAsync(composers, ct)
            )
            .ConfigureAwait(false);

        await RoomTelemetry
            .MeasureAsync(
                RoomTelemetry.MEMBERSHIP,
                roomId,
                () => playerPresence.SetActiveRoomAsync(roomId, ct)
            )
            .ConfigureAwait(false);

        await room.RefreshControllerLevelForPlayerAsync(roomCtx, ct).ConfigureAwait(false);

        // The client answers this with GetGuestRoom(enterRoom: true), which is what populates its
        // in-room info window and the owner-only settings buttons.
        var controllerLevel = await room.GetControllerLevelAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        await _grainFactory
            .SendComposerToPlayerAsync(
                ctx.PlayerId,
                new RoomEntryInfoMessageComposer
                {
                    RoomId = roomId,
                    IsOwner = controllerLevel >= RoomControllerType.Owner,
                },
                ct
            )
            .ConfigureAwait(false);
    }

    public async Task ClickTileAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        await roomGrain.ClickTileAsync(ctx, targetX, targetY, ct).ConfigureAwait(false);
        await roomGrain.WalkAvatarToAsync(ctx, targetX, targetY, ct).ConfigureAwait(false);
    }

    public async Task PickupItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        bool isConfirm = true
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        await roomGrain.RemoveItemByIdAsync(ctx, itemId, ct).ConfigureAwait(false);
    }

    public async Task UseItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        await roomGrain.UseItemByIdAsync(ctx, itemId, ct, param).ConfigureAwait(false);
    }

    public async Task ClickItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        await roomGrain.ClickItemByIdAsync(ctx, itemId, ct, param).ConfigureAwait(false);
    }
}
