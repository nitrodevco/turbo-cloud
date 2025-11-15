using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;
using Turbo.Primitives.Orleans.Snapshots.Room.Settings;
using Turbo.Primitives.Orleans.States.Rooms;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Primitives.Snapshots.Rooms.Extensions;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Mapping;

namespace Turbo.Rooms.Grains;

public class RoomGrain(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    IOptions<RoomConfig> roomConfig,
    ILogger<IRoomGrain> logger,
    IRoomModelProvider roomModelProvider,
    IRoomFloorItemsLoader floorItemsLoader,
    IGrainFactory grainFactory
) : Grain, IRoomGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly RoomConfig _roomConfig = roomConfig.Value;
    private readonly ILogger<IRoomGrain> _logger = logger;
    private readonly IRoomModelProvider _roomModelProvider = roomModelProvider;
    private readonly IRoomFloorItemsLoader _floorItemsLoader = floorItemsLoader;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly RoomState _state = new();

    private IAsyncStream<RoomEvent>? _stream = null;
    private HashSet<long> _connectedPlayerIds = [];
    private RoomSnapshot? _snapshot = null;
    private IRoomMap _roomMap = default!;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            var provider = this.GetStreamProvider(OrleansStreamProviders.DEFAULT_STREAM_PROVIDER);

            _stream = provider.GetStream<RoomEvent>(
                StreamId.Create(OrleansStreamNames.ROOM_EVENTS, this.GetPrimaryKeyLong())
            );

            await HydrateFromExternalAsync(ct);
            await LoadMapAsync(ct);
            await LoadFloorItemsAsync(ct);

            var snapshot = await GetSnapshotAsync();

            await _grainFactory
                .GetGrain<IRoomDirectoryGrain>(0)
                .UpsertActiveRoomAsync(
                    new RoomActiveInfoSnapshot
                    {
                        RoomId = this.GetPrimaryKeyLong(),
                        Population = 0,
                        Name = snapshot.Name,
                        Description = snapshot.Description,
                        OwnerId = snapshot.OwnerId,
                        OwnerName = string.Empty,
                        LastUpdatedUtc = DateTime.UtcNow,
                    }
                );

            _logger.LogInformation("RoomGrain:{RoomId} activated.", this.GetPrimaryKeyLong());
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
            await _grainFactory
                .GetGrain<IRoomDirectoryGrain>(0)
                .MarkInactiveAsync(this.GetPrimaryKeyLong());

            await WriteToDatabaseAsync(ct);

            _logger.LogInformation("RoomGrain {RoomId} deactivated.", this.GetPrimaryKeyLong());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating RoomGrain {RoomId}", this.GetPrimaryKeyLong());

            throw;
        }
    }

    protected async Task HydrateFromExternalAsync(CancellationToken ct)
    {
        using var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

        var entity =
            await dbCtx
                .Rooms.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == this.GetPrimaryKeyLong(), ct)
            ?? throw new Exception(
                $"Room with ID {this.GetPrimaryKeyLong()} not found in database."
            );

        _snapshot = new RoomSnapshot
        {
            Id = entity.Id,
            Name = entity.Name ?? string.Empty,
            Description = entity.Description ?? string.Empty,
            OwnerId = (long)entity.PlayerEntityId,
            DoorMode = entity.DoorMode,
            Password = entity.Password,
            ModelId = entity.RoomModelEntityId,
            CategoryId = entity.NavigatorCategoryEntityId,
            PlayersMax = entity.PlayersMax,
            AllowPets = entity.AllowPets,
            AllowPetsEat = entity.AllowPetsEat,
            TradeMode = entity.TradeType,
            ModSettings = new ModSettingsSnapshot
            {
                WhoCanMute = entity.MuteType,
                WhoCanKick = entity.KickType,
                WhoCanBan = entity.BanType,
            },
            ChatSettings = new ChatSettingsSnapshot
            {
                ChatMode = entity.ChatModeType,
                BubbleWidth = entity.ChatBubbleType,
                ScrollSpeed = entity.ChatSpeedType,
                FullHearRange = entity.ChatDistance,
                FloodSensitivity = entity.ChatFloodType,
            },
        };

        _state.IsLoaded = true;
        _state.LastTick = DateTime.UtcNow;
    }

    protected async Task WriteToDatabaseAsync(CancellationToken ct)
    {
        var snapshot = await GetSnapshotAsync();

        using var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            /* await dbCtx
                .Rooms.Where(r => r.Id == this.GetPrimaryKeyLong())
                .ExecuteUpdateAsync(up => { }, ct); */
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error writing RoomGrain {RoomId} to database.",
                this.GetPrimaryKeyLong()
            );

            throw;
        }
    }

    protected Task LoadMapAsync(CancellationToken ct)
    {
        if (_snapshot is null)
            return Task.CompletedTask;

        var model = _roomModelProvider.Current.GetModelById(_snapshot.ModelId);

        _roomMap = new RoomMap(model);

        return Task.CompletedTask;
    }

    protected async Task LoadFloorItemsAsync(CancellationToken ct)
    {
        if (_snapshot is null)
            return;

        var floorItems = await _floorItemsLoader.LoadByRoomIdAsync(_snapshot.Id, ct);

        if (floorItems.Count is 0)
            return;

        foreach (var item in floorItems)
        {
            _roomMap.AddFloorItem(item);
        }
    }

    public Task<ImmutableArray<long>> GetPlayerIdsAsync() =>
        Task.FromResult(_connectedPlayerIds.ToImmutableArray());

    public async Task<bool> AddPlayerIdAsync(long playerId)
    {
        var added = _connectedPlayerIds.Add(playerId);

        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(0)
            .UpdatePopulationAsync(this.GetPrimaryKeyLong(), _connectedPlayerIds.Count);

        return added;
    }

    public async Task<bool> RemovePlayerIdAsync(long playerId)
    {
        var removed = _connectedPlayerIds.Remove(playerId);

        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(0)
            .UpdatePopulationAsync(this.GetPrimaryKeyLong(), _connectedPlayerIds.Count);

        return removed;
    }

    public Task<string> GetWorldTypeAsync() => Task.FromResult(_roomMap?.ModelName ?? string.Empty);

    public Task<RoomSnapshot> GetSnapshotAsync() => Task.FromResult(_snapshot!);

    public Task<RoomMapSnapshot> GetMapSnapshotAsync() =>
        Task.FromResult(
            new RoomMapSnapshot
            {
                ModelName = _roomMap.ModelName,
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

    public Task<IReadOnlyList<RoomFloorItemSnapshot>> GetFloorItemSnapshotsAsync()
    {
        var floorItems = _roomMap.GetAllFloorItems();
        var snapshots = new RoomFloorItemSnapshot[floorItems.Count];

        for (var i = 0; i < floorItems.Count; i++)
        {
            var item = floorItems[i];

            snapshots[i] = RoomFloorItemSnapshot.FromFloorItem(item);
        }

        return Task.FromResult<IReadOnlyList<RoomFloorItemSnapshot>>(snapshots);
    }
}
