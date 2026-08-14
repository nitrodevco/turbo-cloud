using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
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
using Turbo.Primitives.Rooms.Admin;
using Turbo.Primitives.Rooms.Enums;
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
    internal readonly IRoomObjectLogicProvider _logicProvider;
    internal readonly IRoomAvatarProvider _avatarProvider;
    internal readonly IRoomWiredVariablesProvider _wiredVariablesProvider;
    internal readonly IGrainFactory _grainFactory;

    internal IAsyncStream<RoomOutbound> _roomOutbound = default!;

    internal readonly RoomLiveState _state;

    public readonly RoomEventModule EventModule;
    public readonly RoomSecurityModule SecurityModule;
    public readonly RoomMapModule MapModule;
    public readonly RoomObjectModule ObjectModule;
    public readonly RoomAvatarModule AvatarModule;
    public readonly RoomFurniModule FurniModule;
    public readonly RoomActionModule ActionModule;

    public readonly RoomPathingSystem PathingSystem;
    public readonly RoomAvatarTickSystem AvatarTickSystem;
    public readonly RoomRollerSystem RollerSystem;
    public readonly RoomWiredSystem WiredSystem;
    public readonly RoomChatSystem ChatSystem;

    public RoomId RoomId => _state.RoomId;

    public RoomGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<RoomConfig> roomConfig,
        ILogger<IRoomGrain> logger,
        IRoomModelProvider roomModelProvider,
        IRoomItemsProvider itemsLoader,
        IRoomObjectLogicProvider logicProvider,
        IRoomAvatarProvider avatarProvider,
        IRoomWiredVariablesProvider wiredVariablesProvider,
        IGrainFactory grainFactory
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _roomConfig = roomConfig.Value;
        _logger = logger;
        _roomModelProvider = roomModelProvider;
        _itemsLoader = itemsLoader;
        _logicProvider = logicProvider;
        _avatarProvider = avatarProvider;
        _wiredVariablesProvider = wiredVariablesProvider;
        _grainFactory = grainFactory;

        _state = new() { RoomId = (RoomId)this.GetPrimaryKeyLong() };
        PathingSystem = new(this);
        EventModule = new(this);
        SecurityModule = new(this);
        MapModule = new(this);
        ObjectModule = new(this);
        AvatarModule = new(this);
        FurniModule = new(this);
        ActionModule = new(this);

        AvatarTickSystem = new(this);
        RollerSystem = new(this);
        WiredSystem = new(this);
        ChatSystem = new(this);

        EventModule.Register(RollerSystem);
        EventModule.Register(WiredSystem);
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        if (_state.EpochMs == 0)
        {
            var now = NowMs();

            _state.EpochMs = now;
            _state.NextAvatarBoundaryMs = AlignToNextBoundary(now, _roomConfig.AvatarTickMs);
            _state.NextRollerBoundaryMs = AlignToNextBoundary(now, _roomConfig.RollerTickMs);
            _state.NextWiredBoundaryMs = AlignToNextBoundary(now, _roomConfig.WiredTickMs);
        }

        await HydrateRoomStateAsync(ct);

        await _grainFactory.GetRoomDirectoryGrain().UpsertActiveRoomAsync(_state.RoomSnapshot);

        var provider = this.GetStreamProvider(OrleansStreamProviders.ROOM_STREAM_PROVIDER);

        var streamId = StreamId.Create(OrleansStreamNames.ROOM_STREAM, this.GetPrimaryKeyLong());

        _roomOutbound = provider.GetStream<RoomOutbound>(streamId);

        this.RegisterGrainTimer<object?>(
            async (state, ct) =>
            {
                var now = NowMs();

                await AvatarTickSystem.ProcessAvatarsAsync(now, ct);
                await WiredSystem.ProcessWiredAsync(now, ct);
                await RollerSystem.ProcessRollersAsync(now, ct);
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

            await _grainFactory.GetRoomDirectoryGrain().RemoveActiveRoomAsync(_state.RoomId);
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

        await MapModule.EnsureMapBuiltAsync(ct);
        await FurniModule.EnsureFurniLoadedAsync(ct);
    }

    public Task<RoomSnapshot> GetSnapshotAsync() => Task.FromResult(_state.RoomSnapshot);

    public async Task<RoomSummarySnapshot> GetSummaryAsync()
    {
        var population = await GetRoomPopulationAsync();

        return new RoomSummarySnapshot
        {
            RoomId = _state.RoomSnapshot.RoomId,
            Name = _state.RoomSnapshot.Name,
            Description = _state.RoomSnapshot.Description,
            OwnerId = _state.RoomSnapshot.OwnerId,
            OwnerName = _state.RoomSnapshot.OwnerName,
            Population = population,
            LastUpdatedUtc = DateTime.UtcNow,
        };
    }

    public async Task<int> GetRoomPopulationAsync() =>
        await _grainFactory.GetRoomDirectoryGrain().GetRoomPopulationAsync(_state.RoomId);

    public Task<ImmutableArray<KeyValuePair<RoomPropertyType, string>>> GetRoomPropertiesAsync() =>
        Task.FromResult(_state.RoomProperties.ToImmutableArray());

    public Task PublishRoomEventAsync(RoomEvent evt, CancellationToken ct) =>
        EventModule.PublishAsync(evt, ct);

    public Task SendComposerToRoomAsync(IComposer composer) =>
        _roomOutbound.OnNextAsync(new RoomOutbound { RoomId = _state.RoomId, Composer = composer });

    public async Task AdminUpdateSettingsAsync(RoomAdminSettingsUpdate update, CancellationToken ct)
    {
        _state.RoomSnapshot = _state.RoomSnapshot with
        {
            Name = update.Name,
            Description = update.Description,
            DoorMode = update.DoorMode,
            Password = update.Password,
            PlayersMax = update.PlayersMax,
            ModSettings = _state.RoomSnapshot.ModSettings with
            {
                WhoCanMute = update.WhoCanMute,
                WhoCanKick = update.WhoCanKick,
                WhoCanBan = update.WhoCanBan,
            },
            LastUpdatedUtc = DateTime.UtcNow,
        };

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .Rooms.Where(x => x.Id == (int)_state.RoomId.Value)
            .ExecuteUpdateAsync(
                up =>
                    up.SetProperty(r => r.Name, update.Name)
                        .SetProperty(r => r.Description, update.Description)
                        .SetProperty(r => r.DoorMode, update.DoorMode)
                        .SetProperty(r => r.Password, update.Password)
                        .SetProperty(r => r.PlayersMax, update.PlayersMax)
                        .SetProperty(r => r.MuteType, update.WhoCanMute)
                        .SetProperty(r => r.KickType, update.WhoCanKick)
                        .SetProperty(r => r.BanType, update.WhoCanBan),
                ct
            )
            .ConfigureAwait(false);

        await _grainFactory
            .GetRoomDirectoryGrain()
            .UpsertActiveRoomAsync(_state.RoomSnapshot)
            .ConfigureAwait(false);
    }

    private async Task HydrateRoomStateAsync(CancellationToken ct)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        try
        {
            var entity =
                await dbCtx
                    .Rooms.AsNoTracking()
                    .SingleOrDefaultAsync(e => e.Id == (int)_state.RoomId.Value, ct)
                ?? throw new TurboException(TurboErrorCodeEnum.RoomNotFound);

            _state.Model = _roomModelProvider.GetModelById(entity.RoomModelEntityId);

            if (!string.IsNullOrEmpty(entity.PaintWall))
            {
                _state.RoomProperties[RoomPropertyType.Wall] = entity.PaintWall;
            }

            if (!string.IsNullOrEmpty(entity.PaintFloor))
            {
                _state.RoomProperties[RoomPropertyType.Floor] = entity.PaintFloor;
            }

            if (!string.IsNullOrEmpty(entity.PaintLandscape))
            {
                _state.RoomProperties[RoomPropertyType.Landscape] = entity.PaintLandscape;
            }

            _state.RoomSnapshot = new RoomSnapshot
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
                WorldType = _state.Model.Name,
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
        var delta = now - _state.EpochMs;
        var mod = delta % offset;

        return mod == 0 ? now : now + (offset - mod);
    }
}
