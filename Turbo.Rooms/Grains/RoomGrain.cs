using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Orleans.Events.Rooms;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Room.Settings;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain : Grain, IRoomGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory;
    private readonly RoomConfig _roomConfig;
    private readonly IRoomModelProvider _roomModelProvider;
    private readonly IRoomItemsLoader _itemsLoader;
    private readonly IFurnitureLogicFactory _furnitureLogicFactory;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IRoomGrain> _logger;

    private readonly IPersistentState<RoomState> _state;
    private readonly RoomLiveState _liveState;
    private readonly RoomMapModule _mapModule;
    private readonly RoomFurniModule _furniModule;

    private IAsyncStream<RoomEvent>? _stream = null;

    public RoomGrain(
        [PersistentState(OrleansStateNames.ROOM_STATE, OrleansStorageNames.ROOM_STORE)]
            IPersistentState<RoomState> state,
        IDbContextFactory<TurboDbContext> dbContextFactory,
        IOptions<RoomConfig> roomConfig,
        IRoomModelProvider roomModelProvider,
        IRoomItemsLoader itemsLoader,
        IFurnitureLogicFactory furnitureLogicFactory,
        IGrainFactory grainFactory,
        ILogger<IRoomGrain> logger
    )
    {
        _dbContextFactory = dbContextFactory;
        _roomConfig = roomConfig.Value;
        _roomModelProvider = roomModelProvider;
        _itemsLoader = itemsLoader;
        _furnitureLogicFactory = furnitureLogicFactory;
        _grainFactory = grainFactory;
        _logger = logger;

        _state = state;
        _liveState = new();
        _mapModule = new RoomMapModule(this, _roomConfig, _liveState);
        _furniModule = new RoomFurniModule(
            this,
            _roomConfig,
            _liveState,
            _mapModule,
            _itemsLoader,
            _furnitureLogicFactory
        );
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var provider = this.GetStreamProvider(OrleansStreamProviders.DEFAULT_STREAM_PROVIDER);

        _stream = provider.GetStream<RoomEvent>(
            StreamId.Create(OrleansStreamNames.ROOM_EVENTS, this.GetPrimaryKeyLong())
        );

        await HydrateRoomStateAsync(ct);

        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(RoomDirectoryGrain.SINGLETON_KEY)
            .UpsertActiveRoomAsync(_state.State.RoomSnapshot);

        await _mapModule.OnActivateAsync(ct);
        await _furniModule.OnActivateAsync(ct);

        this.RegisterGrainTimer<object?>(
            async _ => await _mapModule.FlushDirtyTileIdsAsync(ct),
            null,
            TimeSpan.FromMilliseconds(_roomConfig.DirtyTilesFlushIntervalMilliseconds),
            TimeSpan.FromMilliseconds(_roomConfig.DirtyTilesFlushIntervalMilliseconds)
        );

        this.RegisterGrainTimer<object?>(
            async _ => await _furniModule.FlushDirtyItemIdsAsync(_dbContextFactory, ct),
            null,
            TimeSpan.FromMilliseconds(_roomConfig.DirtyItemsFlushIntervalMilliseconds),
            TimeSpan.FromMilliseconds(_roomConfig.DirtyItemsFlushIntervalMilliseconds)
        );
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        await _furniModule.FlushDirtyItemIdsAsync(_dbContextFactory, ct);
        await _furniModule.OnDeactivateAsync(ct);
        await _mapModule.OnDeactivateAsync(ct);

        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(RoomDirectoryGrain.SINGLETON_KEY)
            .RemoveActiveRoomAsync(this.GetPrimaryKeyLong());

        _logger.LogInformation(
            "RoomGrain:{RoomId} deactivated. Reason: {Reason}",
            this.GetPrimaryKeyLong(),
            reason
        );
    }

    public void DeactivateRoom() => DeactivateOnIdle();

    public void DelayRoomDeactivation() =>
        DelayDeactivation(TimeSpan.FromMilliseconds(_roomConfig.RoomDeactivationDelayMilliseconds));

    public async Task EnsureRoomActiveAsync(CancellationToken ct)
    {
        await _mapModule.EnsureMapBuiltAsync(ct);
        await _furniModule.EnsureFurniLoadedAsync(ct);
        await _mapModule.EnsureMapCompiledAsync(ct);
    }

    public Task<RoomSnapshot> GetSnapshotAsync() => Task.FromResult(_state.State.RoomSnapshot);

    public async Task<RoomSummarySnapshot> GetSummaryAsync()
    {
        var population = await GetRoomPopulationAsync();

        return new RoomSummarySnapshot
        {
            RoomId = _state.State.RoomSnapshot.RoomId,
            Name = _state.State.RoomSnapshot.Name,
            Description = _state.State.RoomSnapshot.Description,
            OwnerId = _state.State.RoomSnapshot.OwnerId,
            OwnerName = _state.State.RoomSnapshot.OwnerName,
            Population = population,
            LastUpdatedUtc = DateTime.UtcNow,
        };
    }

    public async Task<int> GetRoomPopulationAsync() =>
        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(RoomDirectoryGrain.SINGLETON_KEY)
            .GetRoomPopulationAsync(this.GetPrimaryKeyLong());

    public async Task SendComposerToRoomAsync(IComposer composer, CancellationToken ct)
    {
        var roomDirectory = _grainFactory.GetGrain<IRoomDirectoryGrain>(
            RoomDirectoryGrain.SINGLETON_KEY
        );

        await roomDirectory.SendComposerToRoomAsync(composer, this.GetPrimaryKeyLong(), ct);
    }

    private async Task HydrateRoomStateAsync(CancellationToken ct)
    {
        if (_state.State.IsLoaded)
            return;

        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            var entity =
                await dbCtx
                    .Rooms.AsNoTracking()
                    .SingleOrDefaultAsync(e => e.Id == this.GetPrimaryKeyLong(), ct)
                ?? throw new Exception(
                    $"RoomGrain:{this.GetPrimaryKeyLong()} not found in database."
                );

            _liveState.Model = _roomModelProvider.GetModelById(entity.RoomModelEntityId);

            _state.State.RoomSnapshot = new RoomSnapshot
            {
                RoomId = entity.Id,
                Name = entity.Name ?? string.Empty,
                Description = entity.Description ?? string.Empty,
                OwnerId = (long)entity.PlayerEntityId,
                OwnerName = string.Empty,
                Population = 0,
                DoorMode = entity.DoorMode,
                PlayersMax = entity.PlayersMax,
                TradeType = entity.TradeType,
                Score = 0,
                Ranking = 0,
                CategoryId = entity.NavigatorCategoryEntityId ?? -1,
                Tags = [],
                AllowPets = entity.AllowPets,
                AllowPetsEat = entity.AllowPetsEat,
                Password = entity.Password ?? string.Empty,
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
                WorldType = _liveState.Model.Name,
                LastUpdatedUtc = DateTime.UtcNow,
            };

            _state.State.IsLoaded = true;

            await _state.WriteStateAsync(ct);
        }
        catch (Exception e)
        {
            throw;
        }
        finally
        {
            await dbCtx.DisposeAsync();
        }
    }
}
