using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
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

    public async Task OpenRoomForPlayerIdAsync(
        ActionContext ctx,
        PlayerId playerId,
        RoomId roomId,
        CancellationToken ct
    )
    {
        try
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

            await playerPresence
                .SendComposerAsync(new OpenConnectionMessageComposer { RoomId = roomId })
                .ConfigureAwait(false);

            await room.EnsureRoomActiveAsync(ct).ConfigureAwait(false);

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
                    },
                    new RoomVisualizationSettingsMessageComposer
                    {
                        WallsHidden = _roomConfig.DefaultWallsHidden,
                        WallThickness = _roomConfig.DefaultWallThickness,
                        FloorThickness = _roomConfig.DefaultFloorThickness,
                    }
                )
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
                    new ItemsMessageComposer
                    {
                        OwnerNames = ownersSnapshot,
                        WallItems = wallSnapshot,
                    },
                    new UsersMessageComposer { Avatars = avatarSnapshots },
                    new UserUpdateMessageComposer { Avatars = avatarSnapshots }
                )
                .ConfigureAwait(false);

            if (danceComposers.Length > 0)
                await playerPresence.SendComposerAsync(danceComposers).ConfigureAwait(false);
            if (effectComposers.Length > 0)
                await playerPresence.SendComposerAsync(effectComposers).ConfigureAwait(false);

            await playerPresence.SetActiveRoomAsync(roomId, ct).ConfigureAwait(false);

            await room.RefreshControllerLevelForPlayerAsync(ctx, ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            throw;
        }
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
