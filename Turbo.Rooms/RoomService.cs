using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms;

internal sealed partial class RoomService(
    ILogger<IRoomService> logger,
    IOptions<RoomConfig> roomConfig,
    ISessionGateway sessionGateway,
    IGrainFactory grainFactory
) : IRoomService
{
    private readonly ILogger<IRoomService> _logger = logger;
    private readonly RoomConfig _roomConfig = roomConfig.Value;
    private readonly ISessionGateway _sessionGateway = sessionGateway;
    private readonly IGrainFactory _grainFactory = grainFactory;

    /// <summary>
    /// Entry flow, as seen by the client:
    /// <list type="bullet">
    /// <item>Navigator: GetGuestRoom(roomForward) already answered; enter only if the door does
    /// not need the client to prompt (locked/password), otherwise stay silent.</item>
    /// <item>Direct: OpenFlatConnection. OpenConnection is sent first so the client leaves the
    /// hotel view, then the door is checked: banned/full → CantConnect + CloseConnection,
    /// wrong or missing password → GenericError + CloseConnection, locked → doorbell ring
    /// (FlatAccessDenied when nobody can answer), otherwise the room is loaded.</item>
    /// <item>Forced: server-driven (teleporter etc.). Same as Direct but the door mode is
    /// ignored.</item>
    /// </list>
    /// </summary>
    public async Task OpenRoomForPlayerIdAsync(
        ActionContext ctx,
        PlayerId playerId,
        RoomId roomId,
        RoomEntryType entryType,
        CancellationToken ct,
        string? password = null
    )
    {
        if (playerId <= 0 || roomId <= 0)
            return;

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(playerId);
        var activeRoom = await playerPresence.GetActiveRoomAsync().ConfigureAwait(false);

        if (activeRoom.RoomId == roomId)
            return;

        await LeavePendingDoorbellAsync(playerPresence, playerId, roomId, ct).ConfigureAwait(false);

        var room = _grainFactory.GetRoomGrain(roomId);

        await room.EnsureRoomActiveAsync(ct).ConfigureAwait(false);

        var access = await room.CheckEntryAccessAsync(
                playerId,
                password,
                bypassDoor: entryType == RoomEntryType.Forced,
                ct
            )
            .ConfigureAwait(false);

        // The navigator result already told the client the door mode; it will show the doorbell
        // or password prompt itself and come back through OpenFlatConnection.
        if (
            entryType == RoomEntryType.Navigator
            && access
                is RoomEntryAccessType.Doorbell
                    or RoomEntryAccessType.PasswordRequired
                    or RoomEntryAccessType.InvalidPassword
        )
            return;

        await playerPresence.ClearActiveRoomAsync(ct).ConfigureAwait(false);

        await playerPresence
            .SendComposerAsync(new OpenConnectionMessageComposer { RoomId = roomId })
            .ConfigureAwait(false);

        switch (access)
        {
            case RoomEntryAccessType.Banned:
                await RejectEntryAsync(
                        playerPresence,
                        new CantConnectMessageComposer
                        {
                            ErrorType = RoomConnectionErrorType.Banned,
                            AdditionalInfo = string.Empty,
                        }
                    )
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.Full:
                await RejectEntryAsync(
                        playerPresence,
                        new CantConnectMessageComposer
                        {
                            ErrorType = RoomConnectionErrorType.RoomFull,
                        }
                    )
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.PasswordRequired:
            case RoomEntryAccessType.InvalidPassword:
                await RejectEntryAsync(
                        playerPresence,
                        new GenericErrorMessage { ErrorCode = RoomGenericErrorType.InvalidPassword }
                    )
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.Doorbell:
                await RingDoorbellAsync(playerPresence, playerId, room, roomId, ct)
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.Allowed:
                await playerPresence.SetPendingRoomAsync(roomId, true).ConfigureAwait(false);
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
                        }
                    )
                    .ConfigureAwait(false);
                return;
        }
    }

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
        var pendingRoom = await ringerPresence.GetPendingRoomAsync().ConfigureAwait(false);

        // The ringer moved on (entered elsewhere, quit, or disconnected) while waiting.
        if (
            pendingRoom.RoomId != ctx.RoomId
            || pendingRoom.Approved
            || !await ringerPresence.HasActiveSessionAsync().ConfigureAwait(false)
        )
        {
            await ringerPresence.SetPendingRoomAsync(-1, false).ConfigureAwait(false);

            return;
        }

        if (!accepted)
        {
            await ringerPresence.SetPendingRoomAsync(-1, false).ConfigureAwait(false);
            await ringerPresence
                .SendComposerAsync(
                    new FlatAccessDeniedMessageComposer
                    {
                        RoomId = ctx.RoomId,
                        Username = string.Empty,
                    }
                )
                .ConfigureAwait(false);

            return;
        }

        await ringerPresence.SetPendingRoomAsync(ctx.RoomId, true).ConfigureAwait(false);

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

        await playerPresence
            .SendComposerAsync(new CloseConnectionMessageComposer())
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

        await playerPresence.SetPendingRoomAsync(roomId, false).ConfigureAwait(false);

        var notified = await room.RingDoorbellAsync(playerId, player.Name, ct)
            .ConfigureAwait(false);

        if (notified)
            return;

        await playerPresence.SetPendingRoomAsync(-1, false).ConfigureAwait(false);
        await playerPresence
            .SendComposerAsync(
                new FlatAccessDeniedMessageComposer { RoomId = roomId, Username = string.Empty }
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
        var pendingRoom = await playerPresence.GetPendingRoomAsync().ConfigureAwait(false);

        if (pendingRoom.RoomId <= 0 || pendingRoom.RoomId == exceptRoomId)
            return;

        if (!pendingRoom.Approved)
            await _grainFactory
                .GetRoomGrain(pendingRoom.RoomId)
                .RemoveDoorbellRingerAsync(playerId, ct)
                .ConfigureAwait(false);

        await playerPresence.SetPendingRoomAsync(-1, false).ConfigureAwait(false);
    }

    private static async Task RejectEntryAsync(IPlayerPresenceGrain playerPresence, IComposer error)
    {
        await playerPresence.SetPendingRoomAsync(-1, false).ConfigureAwait(false);
        await playerPresence
            .SendComposerAsync(error, new CloseConnectionMessageComposer())
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

        var snapshot = await room.GetSnapshotAsync().ConfigureAwait(false);
        var mapSnapshot = await room.GetMapSnapshotAsync(ct).ConfigureAwait(false);
        var ownersSnapshot = await room.GetAllOwnersAsync(ct).ConfigureAwait(false);
        var floorSnapshot = await room.GetAllFloorItemSnapshotsAsync(ct).ConfigureAwait(false);
        var wallSnapshot = await room.GetAllWallItemSnapshotsAsync(ct).ConfigureAwait(false);
        var avatarSnapshots = await room.GetAllAvatarSnapshotsAsync(ct).ConfigureAwait(false);
        var danceComposers = avatarSnapshots
            .OfType<RoomPlayerAvatarSnapshot>()
            .Where(x => x.DanceType != AvatarDanceType.None)
            .Select(x => new DanceMessageComposer
            {
                ObjectId = x.ObjectId,
                DanceType = x.DanceType,
            })
            .ToArray();
        var effectComposers = avatarSnapshots
            .OfType<RoomPlayerAvatarSnapshot>()
            .Where(x => x.EffectId > 0)
            .Select(x => new AvatarEffectMessageComposer
            {
                ObjectId = x.ObjectId,
                EffectId = x.EffectId,
                DelayMilliseconds = 0,
            })
            .ToArray();

        var roomProperties = await room.GetRoomPropertiesAsync().ConfigureAwait(false);
        var roomPropertyComposers = roomProperties
            .Select(x => new RoomPropertyMessageComposer
            {
                Key = RoomPropertyTypeExtensions.GetString(x.Key),
                Value = x.Value,
            })
            .ToArray();

        await playerPresence
            .SendComposerAsync(
                new RoomReadyMessageComposer { WorldType = snapshot.WorldType, RoomId = roomId }
            )
            .ConfigureAwait(false);

        await playerPresence
            .SendComposerAsync(
                new RoomRatingMessageComposer { Rating = 0, CanRate = false },
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
                    FixedWallsHeight = _roomConfig.DefaultWallHeight,
                    ModelData = mapSnapshot.ModelData,
                    AreaHideData = [],
                    CameraInitX = 0, // TODO
                    CameraInitY = 0, // TODO
                    CameraInitZ = 0, // TODO
                },
                new RoomVisualizationSettingsMessageComposer
                {
                    WallsHidden = snapshot.HideWalls,
                    WallThickness = snapshot.WallThickness,
                    FloorThickness = snapshot.FloorThickness,
                },
                new RoomChatSettingsMessageComposer { ChatProtection = snapshot.ChatProtection }
            )
            .ConfigureAwait(false);

        if (await room.GetIsRoomMutedAsync().ConfigureAwait(false))
            await playerPresence
                .SendComposerAsync(new MuteAllInRoomEventMessageComposer { IsMuted = true })
                .ConfigureAwait(false);

        if (roomPropertyComposers.Length > 0)
            await playerPresence.SendComposerAsync(roomPropertyComposers).ConfigureAwait(false);

        await playerPresence
            .SendComposerAsync(
                new ObjectsMessageComposer
                {
                    OwnerNames = ownersSnapshot,
                    FloorItems = floorSnapshot,
                },
                new ItemsMessageComposer { OwnerNames = ownersSnapshot, WallItems = wallSnapshot },
                new UsersMessageComposer { Avatars = avatarSnapshots },
                new UserUpdateMessageComposer { Avatars = avatarSnapshots }
            )
            .ConfigureAwait(false);

        if (danceComposers.Length > 0)
            await playerPresence.SendComposerAsync(danceComposers).ConfigureAwait(false);
        if (effectComposers.Length > 0)
            await playerPresence.SendComposerAsync(effectComposers).ConfigureAwait(false);

        await playerPresence.SetActiveRoomAsync(roomId, ct).ConfigureAwait(false);

        await room.RefreshControllerLevelForPlayerAsync(roomCtx, ct).ConfigureAwait(false);

        var controllerLevel = await room.GetControllerLevelAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        await playerPresence
            .SendComposerAsync(
                new RoomEntryInfoMessageComposer
                {
                    RoomId = roomId,
                    IsOwner = controllerLevel >= RoomControllerType.Owner,
                }
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
