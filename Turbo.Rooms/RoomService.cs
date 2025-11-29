using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
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
        long playerId,
        RoomId roomId,
        CancellationToken ct = default
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
        catch (Exception e) { }
    }

    public async Task EnterPendingRoomForPlayerIdAsync(
        long playerId,
        CancellationToken ct = default
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

            await playerPresence
                .SendComposerAsync(
                    new ObjectsMessageComposer
                    {
                        OwnerNames = ImmutableDictionary<long, string>.Empty,
                        FloorItems = mapSnapshot.FloorItems,
                    },
                    ct
                )
                .ConfigureAwait(false);

            await playerPresence
                .SendComposerAsync(new UsersMessageComposer { Avatars = mapSnapshot.Avatars }, ct)
                .ConfigureAwait(false);

            await playerPresence.SetActiveRoomAsync(pendingRoom.RoomId).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            throw;
        }
    }

    public async Task CloseRoomForPlayerAsync(long playerId)
    {
        if (playerId <= 0)
            return;

        var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);

        await playerPresence.ClearActiveRoomAsync().ConfigureAwait(false);

        await playerPresence
            .SendComposerAsync(new CloseConnectionMessageComposer())
            .ConfigureAwait(false);
    }

    public async Task MoveFloorItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct = default
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId.Value <= 0 || objectId.Value <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

        if (
            await roomGrain
                .MoveFloorItemByIdAsync(ctx, objectId, newX, newY, newRotation, ct)
                .ConfigureAwait(false)
        )
            return;

        var item = await roomGrain
            .GetFloorItemSnapshotByIdAsync(objectId, ct)
            .ConfigureAwait(false);

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
        RoomObjectId objectId,
        int param = -1,
        CancellationToken ct = default
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId.Value <= 0 || objectId.Value <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

        await roomGrain.UseFloorItemByIdAsync(ctx, objectId, param, ct).ConfigureAwait(false);
    }

    public async Task ClickFloorItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int param = -1,
        CancellationToken ct = default
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId.Value <= 0 || objectId.Value <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

        await roomGrain.ClickFloorItemByIdAsync(ctx, objectId, param, ct).ConfigureAwait(false);
    }

    public async Task WalkAvatarToAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int targetX,
        int targetY,
        CancellationToken ct = default
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId.Value <= 0 || objectId.Value <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId.Value);

        await roomGrain
            .WalkAvatarToAsync(ctx, objectId, targetX, targetY, ct)
            .ConfigureAwait(false);
    }
}
