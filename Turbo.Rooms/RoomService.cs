using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Messages.Outgoing.Room.Permissions;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms;

public sealed class RoomService(
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

    public IRoomDirectoryGrain GetRoomDirectory() =>
        _grainFactory.GetGrain<IRoomDirectoryGrain>(RoomDirectoryGrain.SINGLETON_KEY);

    public IRoomGrain GetRoomGrain(long roomId) => _grainFactory.GetGrain<IRoomGrain>(roomId);

    public async Task OpenRoomForPlayerIdAsync(
        ActionContext ctx,
        long playerId,
        RoomId roomId,
        CancellationToken ct
    )
    {
        try
        {
            //await playerPresence.ClearActiveRoomAsync().ConfigureAwait(false);

            var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);
            var pendingRoom = await playerPresence.GetPendingRoomAsync().ConfigureAwait(false);

            if (pendingRoom.RoomId.Value == roomId.Value)
                return;

            await playerPresence.SetPendingRoomAsync(roomId, true).ConfigureAwait(false);
            await playerPresence
                .SendComposerAsync(new OpenConnectionMessageComposer { RoomId = roomId }, ct)
                .ConfigureAwait(false);

            // if owner => auto-approve
            // if banned => reject
            // if full => reject
            // if passworded => reject (for now)
            // if locked => reject (for now)

            var room = GetRoomGrain(roomId.Value);
            var snapshot = await room.GetSnapshotAsync().ConfigureAwait(false);

            await playerPresence
                .SendComposerAsync(
                    new RoomReadyMessageComposer
                    {
                        WorldType = snapshot.WorldType,
                        RoomId = roomId,
                    },
                    ct
                )
                .ConfigureAwait(false);

            await playerPresence
                .SendComposerAsync(
                    new RoomRatingMessageComposer { Rating = 0, CanRate = false },
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception) { }
    }

    public async Task EnterPendingRoomForPlayerIdAsync(
        ActionContext ctx,
        long playerId,
        CancellationToken ct
    )
    {
        try
        {
            var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);
            var pendingRoom = await playerPresence.GetPendingRoomAsync().ConfigureAwait(false);

            if (pendingRoom.RoomId.Value <= 0 || !pendingRoom.Approved)
                return;

            var room = GetRoomGrain(pendingRoom.RoomId.Value);

            await room.EnsureRoomActiveAsync(ct).ConfigureAwait(false);

            var mapSnapshot = await room.GetMapSnapshotAsync(ct).ConfigureAwait(false);

            await playerPresence
                .SendComposerAsync(
                    new RoomEntryTileMessageComposer
                    {
                        X = mapSnapshot.DoorX,
                        Y = mapSnapshot.DoorY,
                        Rotation = mapSnapshot.DoorRotation,
                    },
                    ct
                )
                .ConfigureAwait(false);

            await playerPresence
                .SendComposerAsync(
                    new HeightMapMessageComposer
                    {
                        Width = mapSnapshot.Width,
                        Size = mapSnapshot.Size,
                        Heights = mapSnapshot.TileEncodedHeights,
                    },
                    ct
                )
                .ConfigureAwait(false);

            await playerPresence
                .SendComposerAsync(
                    new FloorHeightMapMessageComposer
                    {
                        ScaleType = _roomConfig.DefaultRoomScale,
                        FixedWallsHeight = _roomConfig.DefaultWallHeight,
                        ModelData = mapSnapshot.ModelData,
                        AreaHideData = [],
                    },
                    ct
                )
                .ConfigureAwait(false);

            var ownersSnapshot = await room.GetAllOwnersAsync(ct).ConfigureAwait(false);
            var furniSnapshot = await room.GetAllFloorItemSnapshotsAsync(ct).ConfigureAwait(false);

            await playerPresence
                .SendComposerAsync(
                    new ObjectsMessageComposer
                    {
                        OwnerNames = ownersSnapshot,
                        FloorItems = furniSnapshot,
                    },
                    ct
                )
                .ConfigureAwait(false);

            var avatarSnapshot = await room.GetAllAvatarSnapshotsAsync(ct).ConfigureAwait(false);

            await playerPresence
                .SendComposerAsync(new UsersMessageComposer { Avatars = avatarSnapshot }, ct)
                .ConfigureAwait(false);
            await playerPresence
                .SendComposerAsync(new UserUpdateMessageComposer { Avatars = avatarSnapshot }, ct)
                .ConfigureAwait(false);

            await playerPresence
                .SendComposerAsync(
                    new YouAreControllerMessageComposer
                    {
                        RoomId = (int)pendingRoom.RoomId.Value,
                        ControllerLevel = RoomControllerType.Owner,
                    },
                    ct
                )
                .ConfigureAwait(false);
            await playerPresence
                .SendComposerAsync(
                    new WiredPermissionsEventMessageComposer { CanModify = true, CanRead = true },
                    ct
                )
                .ConfigureAwait(false);
            await playerPresence
                .SendComposerAsync(
                    new YouAreOwnerMessageComposer { RoomId = (int)pendingRoom.RoomId.Value },
                    ct
                )
                .ConfigureAwait(false);

            await playerPresence.SetActiveRoomAsync(pendingRoom.RoomId, ct).ConfigureAwait(false);

            var playerSnapshot = await _grainFactory
                .GetGrain<IPlayerGrain>(playerId)
                .GetSummaryAsync(ct)
                .ConfigureAwait(false);

            await room.CreateAvatarFromPlayerAsync(ctx, playerSnapshot, ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task CloseRoomForPlayerAsync(long playerId, CancellationToken ct)
    {
        if (playerId <= 0)
            return;

        var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);

        await playerPresence.ClearActiveRoomAsync(ct).ConfigureAwait(false);

        await playerPresence
            .SendComposerAsync(new CloseConnectionMessageComposer(), ct)
            .ConfigureAwait(false);
    }

    public async Task MoveFloorItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId.Value <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

        if (
            await roomGrain
                .MoveFloorItemByIdAsync(ctx, itemId, newX, newY, newRotation, ct)
                .ConfigureAwait(false)
        )
            return;

        var item = await roomGrain.GetFloorItemSnapshotByIdAsync(itemId, ct).ConfigureAwait(false);

        if (item is null)
            return;

        var session = _sessionGateway.GetSession(ctx.SessionKey);

        if (session is not null)
            await session
                .SendComposerAsync(new ObjectUpdateMessageComposer { FloorItem = item }, ct)
                .ConfigureAwait(false);
    }

    public async Task UseFloorItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId.Value <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

        await roomGrain.UseFloorItemByIdAsync(ctx, itemId, ct, param).ConfigureAwait(false);
    }

    public async Task ClickFloorItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId.Value <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

        await roomGrain.ClickFloorItemByIdAsync(ctx, itemId, ct, param).ConfigureAwait(false);
    }

    public async Task WalkAvatarToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId.Value <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

        await roomGrain.WalkAvatarToAsync(ctx, targetX, targetY, ct).ConfigureAwait(false);
    }
}
