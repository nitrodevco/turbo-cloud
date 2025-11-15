using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Contracts.Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Orleans.Events.Rooms;
using Turbo.Primitives.Orleans.Grains.Room;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Primitives.Snapshots.Rooms.Extensions;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Mapping;

namespace Turbo.Rooms.Grains;

public class RoomMapGrain(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    IOptions<RoomConfig> roomConfig,
    ILogger<IRoomMapGrain> logger,
    IRoomModelProvider roomModelProvider,
    IRoomFloorItemsLoader floorItemsLoader
) : Grain, IRoomMapGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly RoomConfig _roomConfig = roomConfig.Value;
    private readonly ILogger<IRoomMapGrain> _logger = logger;
    private readonly IRoomModelProvider _roomModelProvider = roomModelProvider;
    private readonly IRoomFloorItemsLoader _floorItemsLoader = floorItemsLoader;

    private IAsyncStream<RoomMapEvent>? _stream = null;
    private string _worldType = string.Empty;
    private IRoomMap _roomMap = default!;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            var provider = this.GetStreamProvider(OrleansStreamProviders.DEFAULT_STREAM_PROVIDER);

            _stream = provider.GetStream<RoomMapEvent>(
                StreamId.Create(OrleansStreamNames.ROOM_MAP_EVENTS, this.GetPrimaryKeyLong())
            );

            await HydrateFromExternalAsync(ct);

            _logger.LogInformation("RoomMapGrain:{RoomId} activated.", this.GetPrimaryKeyLong());
        }
        catch
        {
            throw;
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("RoomMapGrain:{RoomId} deactivated.", this.GetPrimaryKeyLong());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deactivating RoomMapGrain:{RoomId}",
                this.GetPrimaryKeyLong()
            );

            throw;
        }
    }

    protected async Task HydrateFromExternalAsync(CancellationToken ct)
    {
        using var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

        var entity =
            await dbCtx
                .Rooms.AsNoTracking()
                .Select(x => new { x.Id, x.RoomModelEntityId })
                .SingleOrDefaultAsync(e => e.Id == this.GetPrimaryKeyLong(), ct)
            ?? throw new Exception(
                $"Room with ID {this.GetPrimaryKeyLong()} not found in database."
            );

        var roomModel =
            _roomModelProvider.Current.GetModelById(entity.RoomModelEntityId)
            ?? throw new Exception(
                $"Room model with ID {entity.RoomModelEntityId} not found for Room ID {this.GetPrimaryKeyLong()}."
            );

        _worldType = roomModel.Name;
        _roomMap = new RoomMap(roomModel);

        var floorItems = await _floorItemsLoader.LoadByRoomIdAsync(this.GetPrimaryKeyLong(), ct);

        foreach (var item in floorItems)
            _roomMap.AddFloorItem(item);
    }

    public Task<string> GetWorldTypeAsync() => Task.FromResult(_worldType);

    public Task<RoomMapSnapshot> GetSnapshotAsync() =>
        Task.FromResult(
            new RoomMapSnapshot
            {
                ModelName = _worldType,
                ModelData = _roomMap.ModelData,
                Width = _roomMap.Width,
                Height = _roomMap.Height,
                Size = _roomMap.Size,
                DoorX = _roomMap.DoorX,
                DoorY = _roomMap.DoorY,
                DoorRotation = _roomMap.DoorRotation,
                TileRelativeHeights = _roomMap.TileRelativeHeights,
            }
        );

    public Task<ImmutableArray<RoomFloorItemSnapshot>> GetFloorItemSnapshotsAsync()
    {
        var floorItems = _roomMap.GetAllFloorItems();
        var snapshots = new RoomFloorItemSnapshot[floorItems.Count];

        for (var i = 0; i < floorItems.Count; i++)
        {
            var item = floorItems[i];

            snapshots[i] = RoomFloorItemSnapshot.FromFloorItem(item);
        }

        return Task.FromResult(snapshots.ToImmutableArray());
    }
}
