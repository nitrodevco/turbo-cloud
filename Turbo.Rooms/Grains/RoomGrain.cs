using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Room.Settings;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Factories;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain : Grain, IRoomGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly RoomConfig _roomConfig;
    private readonly IRoomModelProvider _roomModelProvider;
    private readonly IRoomItemsLoader _itemsLoader;
    private readonly IRoomObjectLogicFactory _logicFactory;
    private readonly IRoomAvatarFactory _roomAvatarFactory;
    private readonly IGrainFactory _grainFactory;

    private readonly int _roomId;
    private readonly RoomLiveState _liveState;

    private readonly RoomEventModule _eventModule;
    private readonly RoomSecurityModule _securityModule;
    private readonly RoomMapModule _mapModule;
    private readonly RoomAvatarModule _avatarModule;
    private readonly RoomFurniModule _furniModule;
    private readonly RoomActionModule _actionModule;

    private readonly RoomPathingSystem _pathingSystem;
    private readonly RoomRollerSystem _rollerSystem;

    public RoomGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<RoomConfig> roomConfig,
        IRoomModelProvider roomModelProvider,
        IRoomItemsLoader itemsLoader,
        IRoomObjectLogicFactory logicFactory,
        IRoomAvatarFactory roomAvatarFactory,
        IGrainFactory grainFactory
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _roomConfig = roomConfig.Value;
        _roomModelProvider = roomModelProvider;
        _itemsLoader = itemsLoader;
        _logicFactory = logicFactory;
        _roomAvatarFactory = roomAvatarFactory;
        _grainFactory = grainFactory;

        _roomId = (int)this.GetPrimaryKeyLong();
        _liveState = new() { RoomId = _roomId };
        _pathingSystem = new();
        _eventModule = new(this, _roomConfig, _liveState);
        _securityModule = new(this, _liveState);
        _mapModule = new(this, _roomConfig, _liveState);
        _avatarModule = new(
            this,
            _roomConfig,
            _liveState,
            _pathingSystem,
            _securityModule,
            _mapModule,
            _roomAvatarFactory,
            _logicFactory
        );
        _furniModule = new(
            this,
            _roomConfig,
            _liveState,
            _mapModule,
            _dbCtxFactory,
            _grainFactory,
            _itemsLoader,
            _logicFactory
        );
        _actionModule = new(
            this,
            _liveState,
            _securityModule,
            _furniModule,
            _grainFactory,
            _itemsLoader
        );

        _rollerSystem = new(this, _roomConfig, _liveState, _mapModule);

        _eventModule.Register(_rollerSystem);
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await HydrateRoomStateAsync(ct);

        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(RoomDirectoryGrain.SINGLETON_KEY)
            .UpsertActiveRoomAsync(_liveState.RoomSnapshot);

        this.RegisterGrainTimer<object?>(
            async _ => await _mapModule.FlushDirtyTileIdxsAsync(ct),
            null,
            TimeSpan.FromMilliseconds(_roomConfig.DirtyTilesFlushIntervalMilliseconds),
            TimeSpan.FromMilliseconds(_roomConfig.DirtyTilesFlushIntervalMilliseconds)
        );

        this.RegisterGrainTimer<object?>(
            async _ => await _furniModule.FlushDirtyItemIdsAsync(ct),
            null,
            TimeSpan.FromMilliseconds(_roomConfig.DirtyItemsFlushIntervalMilliseconds),
            TimeSpan.FromMilliseconds(_roomConfig.DirtyItemsFlushIntervalMilliseconds)
        );

        this.RegisterGrainTimer<object?>(
            async _ =>
            {
                await _avatarModule.FlushDirtyAvatarsAsync(ct);
                await _rollerSystem.ProcessRollersAsync(ct);
            },
            null,
            TimeSpan.FromMilliseconds(_roomConfig.RoomTickMilliseconds),
            TimeSpan.FromMilliseconds(_roomConfig.RoomTickMilliseconds)
        );
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        try
        {
            await _furniModule.FlushDirtyItemIdsAsync(ct);

            await _grainFactory
                .GetGrain<IRoomDirectoryGrain>(RoomDirectoryGrain.SINGLETON_KEY)
                .RemoveActiveRoomAsync(_roomId);
        }
        catch (Exception)
        {
            return;
        }
    }

    public void DeactivateRoom() => DeactivateOnIdle();

    public void DelayRoomDeactivation() =>
        DelayDeactivation(TimeSpan.FromMilliseconds(_roomConfig.RoomDeactivationDelayMilliseconds));

    public async Task EnsureRoomActiveAsync(CancellationToken ct)
    {
        DelayRoomDeactivation();

        await _mapModule.EnsureMapBuiltAsync(ct);
        await _furniModule.EnsureFurniLoadedAsync(ct);
    }

    public Task<RoomSnapshot> GetSnapshotAsync() => Task.FromResult(_liveState.RoomSnapshot);

    public async Task<RoomSummarySnapshot> GetSummaryAsync()
    {
        var population = await GetRoomPopulationAsync();

        return new RoomSummarySnapshot
        {
            RoomId = _liveState.RoomSnapshot.RoomId,
            Name = _liveState.RoomSnapshot.Name,
            Description = _liveState.RoomSnapshot.Description,
            OwnerId = _liveState.RoomSnapshot.OwnerId,
            OwnerName = _liveState.RoomSnapshot.OwnerName,
            Population = population,
            LastUpdatedUtc = DateTime.UtcNow,
        };
    }

    public async Task<int> GetRoomPopulationAsync() =>
        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(RoomDirectoryGrain.SINGLETON_KEY)
            .GetRoomPopulationAsync(_roomId);

    public Task PublishRoomEventAsync(RoomEvent @event, CancellationToken ct) =>
        _eventModule.PublishAsync(@event, ct);

    public async Task SendComposerToRoomAsync(IComposer composer, CancellationToken ct)
    {
        var roomDirectory = _grainFactory.GetGrain<IRoomDirectoryGrain>(
            RoomDirectoryGrain.SINGLETON_KEY
        );

        await roomDirectory.SendComposerToRoomAsync(composer, _roomId, ct);
    }

    private async Task HydrateRoomStateAsync(CancellationToken ct)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        try
        {
            var entity =
                await dbCtx.Rooms.AsNoTracking().SingleOrDefaultAsync(e => e.Id == _roomId, ct)
                ?? throw new TurboException(TurboErrorCodeEnum.RoomNotFound);

            _liveState.Model = _roomModelProvider.GetModelById(entity.RoomModelEntityId);

            _liveState.RoomSnapshot = new RoomSnapshot
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
                AllowBlocking = entity.AllowBlocking,
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
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            await dbCtx.DisposeAsync();
        }
    }
}
