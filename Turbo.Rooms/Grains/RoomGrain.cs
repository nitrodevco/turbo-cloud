using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums;
using Turbo.Database.Context;
using Turbo.Logging;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Room.Settings;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain : Grain, IRoomGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory;
    private readonly RoomConfig _roomConfig;
    private readonly IRoomModelProvider _roomModelProvider;
    private readonly IRoomItemsLoader _itemsLoader;
    private readonly IRoomObjectLogicFactory _logicFactory;
    private readonly IRoomAvatarFactory _roomAvatarFactory;
    private readonly IGrainFactory _grainFactory;

    private readonly RoomId _roomId;
    private readonly RoomLiveState _liveState;
    private readonly RoomEventModule _eventModule;
    private readonly RoomMapModule _mapModule;
    private readonly RoomAvatarModule _avatarModule;
    private readonly RoomFurniModule _furniModule;
    private readonly RoomActionModule _actionModule;

    public RoomGrain(
        IDbContextFactory<TurboDbContext> dbContextFactory,
        IOptions<RoomConfig> roomConfig,
        IRoomModelProvider roomModelProvider,
        IRoomItemsLoader itemsLoader,
        IRoomObjectLogicFactory logicFactory,
        IRoomAvatarFactory roomAvatarFactory,
        IGrainFactory grainFactory
    )
    {
        _dbContextFactory = dbContextFactory;
        _roomConfig = roomConfig.Value;
        _roomModelProvider = roomModelProvider;
        _itemsLoader = itemsLoader;
        _logicFactory = logicFactory;
        _roomAvatarFactory = roomAvatarFactory;
        _grainFactory = grainFactory;

        _roomId = RoomId.From(this.GetPrimaryKeyLong());
        _liveState = new();
        _eventModule = new(this, _roomConfig, _liveState);
        _mapModule = new(this, _roomConfig, _liveState);
        _avatarModule = new(
            this,
            _roomConfig,
            _liveState,
            _mapModule,
            _roomAvatarFactory,
            _logicFactory
        );
        _furniModule = new(this, _roomConfig, _liveState, _mapModule, _itemsLoader, _logicFactory);
        _actionModule = new(this, _liveState, _furniModule);

        _eventModule.Register(new WiredController());
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await HydrateRoomStateAsync(ct);

        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(RoomDirectoryGrain.SINGLETON_KEY)
            .UpsertActiveRoomAsync(_liveState.RoomSnapshot);

        this.RegisterGrainTimer<object?>(
            async _ => await _mapModule.FlushDirtyHeightTileIdsAsync(ct),
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

        this.RegisterGrainTimer<object?>(
            async _ => await _avatarModule.FlushDirtyAvatarsAsync(ct),
            null,
            TimeSpan.FromMilliseconds(_roomConfig.DirtyAvatarsFlushIntervalMilliseconds),
            TimeSpan.FromMilliseconds(_roomConfig.DirtyAvatarsFlushIntervalMilliseconds)
        );
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        await _furniModule.FlushDirtyItemIdsAsync(_dbContextFactory, ct);

        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(RoomDirectoryGrain.SINGLETON_KEY)
            .RemoveActiveRoomAsync(_roomId);
    }

    public void DeactivateRoom() => DeactivateOnIdle();

    public void DelayRoomDeactivation() =>
        DelayDeactivation(TimeSpan.FromMilliseconds(_roomConfig.RoomDeactivationDelayMilliseconds));

    public async Task EnsureRoomActiveAsync(CancellationToken ct)
    {
        DelayRoomDeactivation();

        await _mapModule.EnsureMapBuiltAsync(ct);
        await _furniModule.EnsureFurniLoadedAsync(ct);

        var player = _grainFactory.GetGrain<IPlayerGrain>(_liveState.RoomSnapshot.OwnerId);
        var summary = await player.GetSummaryAsync(ct);

        await _avatarModule.CreateAvatarFromPlayerAsync(summary);
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
        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            var entity =
                await dbCtx
                    .Rooms.AsNoTracking()
                    .SingleOrDefaultAsync(e => e.Id == _roomId.Value, ct)
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
