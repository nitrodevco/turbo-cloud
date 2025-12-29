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
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
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

    public async Task OpenRoomForPlayerIdAsync(
        ActionContext ctx,
        PlayerId playerId,
        RoomId roomId,
        CancellationToken ct
    )
    {
        var playerPresence = _grainFactory.GetPlayerPresenceGrain(playerId);
        var pendingRoom = await playerPresence.GetPendingRoomAsync().ConfigureAwait(false);

        if (pendingRoom.RoomId == roomId)
            return;

        await playerPresence.ClearActiveRoomAsync(ct).ConfigureAwait(false);
        await playerPresence.SetPendingRoomAsync(roomId, true).ConfigureAwait(false);

        // if owner => auto-approve
        // if banned => reject
        // if full => reject
        // if passworded => reject (for now)
        // if locked => reject (for now)

        var room = _grainFactory.GetRoomGrain(roomId);
        var snapshot = await room.GetSnapshotAsync().ConfigureAwait(false);

        await playerPresence
            .SendComposerAsync(
                new OpenConnectionMessageComposer { RoomId = roomId },
                new RoomReadyMessageComposer
                {
                    WorldType = snapshot.WorldType,
                    RoomId = roomId,
                },
                new RoomRatingMessageComposer { Rating = 0, CanRate = false }
            )
            .ConfigureAwait(false);
    }

    public async Task EnterPendingRoomForPlayerIdAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var playerPresence = _grainFactory.GetPlayerPresenceGrain(playerId);
        var pendingRoom = await playerPresence.GetPendingRoomAsync().ConfigureAwait(false);

        if (pendingRoom.RoomId <= 0 || !pendingRoom.Approved)
            return;

        var room = _grainFactory.GetRoomGrain(pendingRoom.RoomId);

        await room.EnsureRoomActiveAsync(ct).ConfigureAwait(false);

        var mapSnapshot = await room.GetMapSnapshotAsync(ct).ConfigureAwait(false);
        var ownersSnapshot = await room.GetAllOwnersAsync(ct).ConfigureAwait(false);
        var floorSnapshot = await room.GetAllFloorItemSnapshotsAsync(ct).ConfigureAwait(false);
        var wallSnapshot = await room.GetAllWallItemSnapshotsAsync(ct).ConfigureAwait(false);

        await playerPresence
            .SendComposerAsync(
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
                },
                new ObjectsMessageComposer
                {
                    OwnerNames = ownersSnapshot,
                    FloorItems = floorSnapshot,
                },
                new ItemsMessageComposer
                {
                    OwnerNames = ownersSnapshot,
                    WallItems = wallSnapshot,
                }
            )
            .ConfigureAwait(false);

        var avatarSnapshot = await room.GetAllAvatarSnapshotsAsync(ct).ConfigureAwait(false);

        await playerPresence
            .SendComposerAsync(
                new UsersMessageComposer { Avatars = avatarSnapshot },
                new UserUpdateMessageComposer { Avatars = avatarSnapshot },
                new YouAreControllerMessageComposer
                {
                    RoomId = pendingRoom.RoomId,
                    ControllerLevel = RoomControllerType.Owner,
                },
                new WiredPermissionsEventMessageComposer { CanModify = true, CanRead = true },
                new YouAreOwnerMessageComposer { RoomId = pendingRoom.RoomId }
            )
            .ConfigureAwait(false);

        await playerPresence.SetActiveRoomAsync(pendingRoom.RoomId, ct).ConfigureAwait(false);
    }

    public async Task CloseRoomForPlayerAsync(PlayerId playerId, CancellationToken ct)
    {
        if (playerId <= 0)
            return;

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(playerId);

        await playerPresence.ClearActiveRoomAsync(ct).ConfigureAwait(false);

        await playerPresence
            .SendComposerAsync(new CloseConnectionMessageComposer())
            .ConfigureAwait(false);
    }

    public async Task WalkAvatarToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        await roomGrain.WalkAvatarToAsync(ctx, targetX, targetY, ct).ConfigureAwait(false);
    }
}
