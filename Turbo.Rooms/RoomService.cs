using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Networking.Abstractions.Session;
using Turbo.Primitives.Grains;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Rooms.Abstractions;
using Turbo.Rooms.Configuration;

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

    public async Task<IRoomGrain> GetRoomGrainAsync(long roomId)
    {
        var grain = _grainFactory.GetGrain<IRoomGrain>(roomId);

        return await Task.FromResult(grain).ConfigureAwait(false);
    }

    public async Task OpenRoomForPlayerIdAsync(
        long playerId,
        long roomId,
        CancellationToken ct = default
    )
    {
        try
        {
            var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);
            var pendingRoom = await playerPresence.GetPendingRoomAsync().ConfigureAwait(false);

            if (pendingRoom.RoomId == roomId)
                return;

            // if the player is currently in a room, leave it

            var session = _sessionGateway.GetSessionContextByPlayerId(playerId);

            if (session is null)
                return;

            var roomGrain = await GetRoomGrainAsync(roomId).ConfigureAwait(false);
            var worldType = await roomGrain.GetWorldTypeAsync().ConfigureAwait(false);

            await playerPresence.SetPendingRoomAsync(roomId, true).ConfigureAwait(false);

            // if owner => auto-approve
            // if banned => reject
            // if full => reject
            // if passworded => reject (for now)
            // if locked => reject (for now)

            await session
                .SendComposerAsync(new OpenConnectionMessageComposer { RoomId = (int)roomId }, ct)
                .ConfigureAwait(false);

            await session
                .SendComposerAsync(
                    new RoomReadyMessageComposer { WorldType = worldType, RoomId = (int)roomId },
                    ct
                )
                .ConfigureAwait(false);

            await session
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
            // if the player is currently in a room, leave it

            var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);
            var pendingRoom = await playerPresence.GetPendingRoomAsync().ConfigureAwait(false);

            if (pendingRoom.RoomId <= 0 || !pendingRoom.Approved)
                return;

            var session = _sessionGateway.GetSessionContextByPlayerId(playerId);

            if (session is null)
                return;

            var roomGrain = await GetRoomGrainAsync(pendingRoom.RoomId).ConfigureAwait(false);
            var mapSnapshot = await roomGrain.GetMapSnapshotAsync().ConfigureAwait(false);

            await playerPresence.EnterRoomAsync(pendingRoom.RoomId).ConfigureAwait(false);

            await session
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

            await session
                .SendComposerAsync(
                    new HeightMapMessageComposer
                    {
                        Width = mapSnapshot.Width,
                        Size = mapSnapshot.Size,
                        Heights = mapSnapshot.TileRelativeHeights,
                    },
                    ct
                )
                .ConfigureAwait(false);

            await session
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

            var floorItemSnapshots = await roomGrain
                .GetFloorItemSnapshotsAsync()
                .ConfigureAwait(false);

            await session
                .SendComposerAsync(
                    new ObjectsMessageComposer { OwnerNames = [], FloorItems = floorItemSnapshots },
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception e) { }
    }
}
