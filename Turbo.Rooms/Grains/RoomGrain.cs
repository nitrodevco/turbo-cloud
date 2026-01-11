using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Database.Context;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Room.Settings;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain : Grain, IRoomGrain
{
    internal readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    internal readonly RoomConfig _roomConfig;
    internal readonly ILogger<IRoomGrain> _logger;
    internal readonly IRoomModelProvider _roomModelProvider;
    internal readonly IRoomItemsProvider _itemsLoader;
    internal readonly IRoomObjectLogicProvider _logicFactory;
    internal readonly IRoomAvatarProvider _roomAvatarFactory;
    internal readonly IGrainFactory _grainFactory;

    internal IAsyncStream<RoomOutbound> _roomOutbound = default!;

    internal readonly RoomId _roomId;
    internal readonly RoomLiveState _liveState;

    internal readonly RoomEventModule _eventModule;
    internal readonly RoomSecurityModule _securityModule;
    internal readonly RoomMapModule _mapModule;
    internal readonly RoomAvatarModule _avatarModule;
    internal readonly RoomFurniModule _furniModule;
    internal readonly RoomActionModule _actionModule;

    internal readonly RoomPathingSystem _pathingSystem;
    internal readonly RoomAvatarTickSystem _avatarTickSystem;
    internal readonly RoomRollerSystem _rollerSystem;
    internal readonly RoomWiredVariableSystem _wiredVariableSystem;
    internal readonly RoomWiredSystem _wiredSystem;

    public RoomGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<RoomConfig> roomConfig,
        ILogger<IRoomGrain> logger,
        IRoomModelProvider roomModelProvider,
        IRoomItemsProvider itemsLoader,
        IRoomObjectLogicProvider logicFactory,
        IRoomAvatarProvider roomAvatarFactory,
        IGrainFactory grainFactory
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _roomConfig = roomConfig.Value;
        _logger = logger;
        _roomModelProvider = roomModelProvider;
        _itemsLoader = itemsLoader;
        _logicFactory = logicFactory;
        _roomAvatarFactory = roomAvatarFactory;
        _grainFactory = grainFactory;

        _roomId = (RoomId)this.GetPrimaryKeyLong();
        _liveState = new() { RoomId = _roomId };
        _pathingSystem = new();
        _eventModule = new(this, _roomConfig, _liveState);
        _securityModule = new(this, _liveState);
        _mapModule = new(this, _roomConfig, _liveState);
        _avatarModule = new(this, _liveState, _pathingSystem, _securityModule, _mapModule);
        _furniModule = new(this, _liveState, _mapModule);
        _actionModule = new(this, _liveState, _securityModule, _furniModule);

        _avatarTickSystem = new(this, _roomConfig, _liveState, _avatarModule, _mapModule);
        _rollerSystem = new(this, _roomConfig, _liveState, _mapModule);
        _wiredVariableSystem = new(this);
        _wiredSystem = new(this, _roomConfig, _liveState, _avatarModule, _mapModule);

        _eventModule.Register(_rollerSystem);
        _eventModule.Register(_wiredVariableSystem);
        _eventModule.Register(_wiredSystem);
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        if (_liveState.EpochMs == 0)
        {
            var now = NowMs();

            _liveState.EpochMs = now;
            _liveState.NextAvatarBoundaryMs = AlignToNextBoundary(now, _roomConfig.AvatarTickMs);
            _liveState.NextRollerBoundaryMs = AlignToNextBoundary(now, _roomConfig.RollerTickMs);
            _liveState.NextWiredBoundaryMs = AlignToNextBoundary(now, _roomConfig.WiredTickMs);
        }

        await HydrateRoomStateAsync(ct);

        await _grainFactory.GetRoomDirectoryGrain().UpsertActiveRoomAsync(_liveState.RoomSnapshot);

        var provider = this.GetStreamProvider(OrleansStreamProviders.ROOM_STREAM_PROVIDER);

        var streamId = StreamId.Create(OrleansStreamNames.ROOM_STREAM, this.GetPrimaryKeyLong());

        _roomOutbound = provider.GetStream<RoomOutbound>(streamId);

        this.RegisterGrainTimer<object?>(
            async (state, ct) =>
            {
                var now = NowMs();

                await _avatarTickSystem.ProcessAvatarsAsync(now, ct);
                await _wiredSystem.ProcessWiredAsync(now, ct);
                await _rollerSystem.ProcessRollersAsync(now, ct);
                await FlushDirtyTilesAsync(ct);
                await FlushDirtyItemsAsync(ct);
            },
            null,
            TimeSpan.FromMilliseconds(_roomConfig.RoomTickMs),
            TimeSpan.FromMilliseconds(_roomConfig.RoomTickMs)
        );
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        try
        {
            await FlushDirtyItemsAsync(ct);

            await _grainFactory.GetRoomDirectoryGrain().RemoveActiveRoomAsync(_roomId);
        }
        catch (Exception)
        {
            return;
        }
    }

    public void DeactivateRoom() => DeactivateOnIdle();

    public void DelayRoomDeactivation() =>
        DelayDeactivation(TimeSpan.FromMilliseconds(_roomConfig.RoomDeactivationDelayMs));

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
        await _grainFactory.GetRoomDirectoryGrain().GetRoomPopulationAsync(_roomId);

    public Task PublishRoomEventAsync(RoomEvent evt, CancellationToken ct) =>
        _eventModule.PublishAsync(evt, ct);

    public Task SendComposerToRoomAsync(IComposer composer) =>
        _roomOutbound.OnNextAsync(new RoomOutbound { RoomId = _roomId, Composer = composer });

    private async Task HydrateRoomStateAsync(CancellationToken ct)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        try
        {
            var entity =
                await dbCtx.Rooms.AsNoTracking().SingleOrDefaultAsync(e => e.Id == (int)_roomId, ct)
                ?? throw new TurboException(TurboErrorCodeEnum.RoomNotFound);

            _liveState.Model = _roomModelProvider.GetModelById(entity.RoomModelEntityId);

            _liveState.RoomSnapshot = new RoomSnapshot
            {
                RoomId = entity.Id,
                Name = entity.Name ?? string.Empty,
                Description = entity.Description ?? string.Empty,
                OwnerId = (PlayerId)entity.PlayerEntityId,
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

    internal long NowMs() => (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);

    internal long AlignToNextBoundary(long now, int offset)
    {
        var delta = now - _liveState.EpochMs;
        var mod = delta % offset;

        return mod == 0 ? now : now + (offset - mod);
    }
}
