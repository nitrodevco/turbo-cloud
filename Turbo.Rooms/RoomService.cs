using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Orleans.Snapshots.Session;
using Turbo.Primitives.Rooms;
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

    public async Task OpenRoomForSessionAsync(
        SessionKey sessionKey,
        long roomId,
        CancellationToken ct = default
    )
    {
        var playerId = _sessionGateway.GetPlayerId(sessionKey);

        if (playerId <= 0)
            return;

        await OpenRoomForPlayerIdAsync(playerId, roomId, ct).ConfigureAwait(false);
    }

    public async Task OpenRoomForPlayerIdAsync(
        long playerId,
        long roomId,
        CancellationToken ct = default
    )
    {
        try
        {
            //await playerPresence.ClearActiveRoomAsync().ConfigureAwait(false);

            var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);
            var pendingRoom = await playerPresence.GetPendingRoomAsync().ConfigureAwait(false);

            if (pendingRoom.RoomId == roomId)
                return;

            await playerPresence.SetPendingRoomAsync(roomId, true).ConfigureAwait(false);
            await playerPresence
                .SendComposerAsync(new OpenConnectionMessageComposer { RoomId = (int)roomId }, ct)
                .ConfigureAwait(false);

            // if owner => auto-approve
            // if banned => reject
            // if full => reject
            // if passworded => reject (for now)
            // if locked => reject (for now)

            var room = GetRoomGrain(roomId);
            var snapshot = await room.GetSnapshotAsync().ConfigureAwait(false);

            await playerPresence
                .SendComposerAsync(
                    new RoomReadyMessageComposer
                    {
                        WorldType = snapshot.WorldType,
                        RoomId = (int)roomId,
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

    public async Task EnterPendingRoomForSessionAsync(
        SessionKey sessionKey,
        CancellationToken ct = default
    )
    {
        var playerId = _sessionGateway.GetPlayerId(sessionKey);

        if (playerId <= 0)
            return;

        await EnterPendingRoomForPlayerIdAsync(playerId, ct).ConfigureAwait(false);
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

            if (pendingRoom.RoomId <= 0 || !pendingRoom.Approved)
                return;

            var room = GetRoomGrain(pendingRoom.RoomId);

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

            await playerPresence.SetActiveRoomAsync(pendingRoom.RoomId).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            throw;
        }
    }

    public async Task CloseRoomForPlayerAsync(long playerId)
    {
        var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);

        await playerPresence.ClearActiveRoomAsync().ConfigureAwait(false);

        await playerPresence
            .SendComposerAsync(new CloseConnectionMessageComposer())
            .ConfigureAwait(false);
    }

    public async Task MoveFloorItemInRoomAsync(
        ActorContext ctx,
        long itemId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct = default
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId);

        var isValidPlacement = await roomGrain
            .ValidateFloorItemPlacementAsync(itemId, newX, newY, newRotation)
            .ConfigureAwait(false);

        if (!isValidPlacement)
        {
            var item = await roomGrain
                .GetFloorItemSnapshotByIdAsync(itemId, ct)
                .ConfigureAwait(false);

            if (item is not null)
            {
                var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(ctx.PlayerId);

                await playerPresence
                    .SendComposerAsync(new ObjectUpdateMessageComposer { FloorItem = item }, ct)
                    .ConfigureAwait(false);
            }

            return;
        }

        await roomGrain
            .MoveFloorItemByIdAsync(itemId, newX, newY, newRotation, ct)
            .ConfigureAwait(false);
    }

    public async Task UseFloorItemInRoomAsync(
        ActorContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId);

        await roomGrain.UseFloorItemByIdAsync(itemId, param, ct).ConfigureAwait(false);
    }

    public async Task ClickFloorItemInRoomAsync(
        ActorContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetGrain<IRoomGrain>(ctx.RoomId);

        await roomGrain.ClickFloorItemByIdAsync(itemId, param, ct).ConfigureAwait(false);
    }
}
