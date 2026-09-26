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
using Turbo.Database.Extensions;
using Turbo.Events;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Texts;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Wired.VariableFx;

namespace Turbo.Rooms.Grains;

/// <summary>
/// Owns a live room: its map, its avatars, its furni and the systems that tick over them.
/// Its own state reaches the database through <see cref="IRoomPersistenceGrain"/> and not from
/// here — the room hands over what changed and carries on, so a write never holds up the tick,
/// and there is nothing to flush on deactivation beyond telling the persistence grain. The
/// settings and the navigator row are the exception: they are written through as they change,
/// because a room is never re-read while it is loaded.
///
/// Unlike every other grain implementation this type is public rather than
/// internal: room object logic and wired variables name it in their constructors, and those are
/// discovered by <c>AssemblyExplorer</c>, which skips non-public types. Internalising this class
/// compiles once those are internal too, but they then go undiscovered and silently register
/// nothing at startup.
/// </summary>
public sealed partial class RoomGrain : Grain, IRoomGrain
{
    internal readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    internal readonly RoomConfig _roomConfig;
    internal readonly PetConfig _petConfig;
    internal readonly BotConfig _botConfig;
    internal readonly WiredConfig _wiredConfig;
    internal readonly ILogger<IRoomGrain> _logger;
    internal readonly IRoomModelProvider _roomModelProvider;
    internal readonly IRoomItemsProvider _itemsLoader;
    internal readonly IRoomNpcProvider _npcProvider;
    internal readonly IRoomObjectLogicProvider _logicProvider;
    internal readonly IRoomAvatarProvider _avatarProvider;
    internal readonly IRoomWiredVariablesProvider _wiredVariablesProvider;
    internal readonly IPetBreedProvider _petBreedProvider;
    internal readonly IFurnitureDefinitionProvider _definitionProvider;
    internal readonly IHotelTextProvider _hotelTextProvider;
    internal readonly ICatalogService _catalogService;
    internal readonly IGrainFactory _grainFactory;
    internal readonly EventSystem _eventSystem;

    internal readonly RoomLiveState _state;

    public readonly RoomEventModule EventModule;
    public readonly RoomSecurityModule SecurityModule;
    public readonly RoomModerationModule ModerationModule;
    public readonly RoomEntryModule EntryModule;
    public readonly RoomMapModule MapModule;
    public readonly RoomObjectModule ObjectModule;
    public readonly RoomAvatarModule AvatarModule;
    public readonly RoomFurniModule FurniModule;
    public readonly RoomActionModule ActionModule;
    public readonly RoomPetModule PetModule;
    public readonly RoomBotModule BotModule;
    public readonly RoomTradeModule TradeModule;

    public readonly RoomPathingSystem PathingSystem;
    public readonly RoomAvatarTickSystem AvatarTickSystem;
    public readonly RoomPetTickSystem PetTickSystem;
    public readonly RoomBotTickSystem BotTickSystem;
    public readonly RoomRollerSystem RollerSystem;
    public readonly RoomWiredSystem WiredSystem;
    public readonly RoomGameSystem GameSystem;
    public readonly RoomVariableFxSystem VariableFxSystem;
    public readonly RoomChatSystem ChatSystem;
    public readonly RoomTimerSystem TimerSystem;

    internal IAsyncStream<RoomOutboundSnapshot> _roomOutbound = default!;
    private IGrainTimer? _roomTimer;

    public RoomId RoomId => _state.RoomId;

    public RoomGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<RoomConfig> roomConfig,
        IOptions<PetConfig> petConfig,
        IOptions<BotConfig> botConfig,
        IOptions<WiredConfig> wiredConfig,
        IGrainFactory grainFactory,
        IRoomModelProvider roomModelProvider,
        IRoomItemsProvider itemsLoader,
        IRoomNpcProvider npcProvider,
        IRoomObjectLogicProvider logicProvider,
        IRoomAvatarProvider avatarProvider,
        IRoomWiredVariablesProvider wiredVariablesProvider,
        IPetBreedProvider petBreedProvider,
        IFurnitureDefinitionProvider definitionProvider,
        IHotelTextProvider hotelTextProvider,
        ICatalogService catalogService,
        EventSystem eventSystem,
        ILogger<IRoomGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _roomConfig = roomConfig.Value;
        _petConfig = petConfig.Value;
        _botConfig = botConfig.Value;
        _wiredConfig = wiredConfig.Value;
        _logger = logger;
        _roomModelProvider = roomModelProvider;
        _itemsLoader = itemsLoader;
        _npcProvider = npcProvider;
        _logicProvider = logicProvider;
        _avatarProvider = avatarProvider;
        _wiredVariablesProvider = wiredVariablesProvider;
        _petBreedProvider = petBreedProvider;
        _definitionProvider = definitionProvider;
        _hotelTextProvider = hotelTextProvider;
        _catalogService = catalogService;
        _grainFactory = grainFactory;
        _eventSystem = eventSystem;

        _state = new() { RoomId = this.GetRoomId() };
        PathingSystem = new(this);
        EventModule = new(this);
        SecurityModule = new(this, _dbCtxFactory);
        ModerationModule = new(this, _dbCtxFactory);
        EntryModule = new(this, _dbCtxFactory);
        MapModule = new(this);
        ObjectModule = new(this);
        AvatarModule = new(this);
        FurniModule = new(this);
        ActionModule = new(this);
        PetModule = new(this);
        BotModule = new(this);
        TradeModule = new(this);

        AvatarTickSystem = new(this);
        PetTickSystem = new(this);
        BotTickSystem = new(this);
        RollerSystem = new(this);
        WiredSystem = new(this);
        GameSystem = new(this);
        VariableFxSystem = new(this);
        ChatSystem = new(this);
        TimerSystem = new(this);

        EventModule.Register(RollerSystem);
        EventModule.Register(WiredSystem);
        EventModule.Register(GameSystem);
        EventModule.Register(VariableFxSystem);
        FurniModule.RegisterPlacementLimit(WiredSystem);
        FurniModule.RegisterPlacementLimit(VariableFxSystem);
        EventModule.Register(ChatSystem);
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        if (_state.EpochMs == 0)
        {
            var now = NowMs();

            _state.EpochMs = now;
            _state.NextAvatarBoundaryMs = AlignToNextBoundary(now, _roomConfig.AvatarTickMs);
            _state.NextRollerBoundaryMs = AlignToNextBoundary(now, _roomConfig.RollerTickMs);
            _state.NextWiredBoundaryMs = AlignToNextBoundary(now, _wiredConfig.TickMs);
        }

        await HydrateRoomStateAsync(ct);

        await _grainFactory.GetRoomDirectoryGrain().UpsertActiveRoomAsync(_state.RoomSnapshot, ct);

        var provider = this.GetStreamProvider(OrleansStreamProviders.ROOM_STREAM_PROVIDER);

        var streamId = StreamId.Create(OrleansStreamNames.ROOM_STREAM, _state.RoomId.Value);

        _roomOutbound = provider.GetStream<RoomOutboundSnapshot>(streamId);

        // One-shot timer re-armed to the next epoch-aligned boundary after each tick.
        // A periodic grain timer measures its period from the end of the previous callback,
        // so tick phase would drift by the callback's execution time and the avatar/wired/roller
        // boundaries (all multiples of RoomTickMs from EpochMs) would be crossed late by a
        // varying amount each cycle.
        _roomTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((RoomGrain)self!).ProcessRoomTickAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_roomConfig.RoomTickMs),
            Timeout.InfiniteTimeSpan
        );
    }

    private async Task ProcessRoomTickAsync(CancellationToken ct)
    {
        try
        {
            var now = NowMs();

            // Pets and bots pick their next walk before avatars step, so it starts this tick.
            await PetTickSystem.ProcessPetsAsync(now, ct);
            await BotTickSystem.ProcessBotsAsync(now, ct);
            await AvatarTickSystem.ProcessAvatarsAsync(now, ct);
            await WiredSystem.ProcessWiredAsync(now, ct);
            await VariableFxSystem.ProcessAsync(now, ct);
            await RollerSystem.ProcessRollersAsync(now, ct);
            await TimerSystem.ProcessTimersAsync(now, ct);
            await FlushDirtyTilesAsync(ct);
            await FlushDirtyItemsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Room {RoomId} failed to process a tick", _state.RoomId);
        }
        finally
        {
            // Always re-arm: a failed tick must not stop the room.
            RearmRoomTimer();
        }
    }

    private void RearmRoomTimer()
    {
        var now = NowMs();
        var next = AlignToNextBoundary(now, _roomConfig.RoomTickMs);

        // AlignToNextBoundary returns `now` when it lands exactly on a boundary; firing again
        // with a zero due time would double-tick the same boundary.
        if (next <= now)
            next = now + _roomConfig.RoomTickMs;

        _roomTimer?.Change(TimeSpan.FromMilliseconds(next - now), Timeout.InfiniteTimeSpan);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _roomTimer?.Dispose();
        _roomTimer = null;

        // Each step is isolated: a failed flush must not leave the room listed as active.
        try
        {
            await FlushDirtyItemsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to flush dirty items of room {RoomId} on deactivation",
                _state.RoomId
            );
        }

        try
        {
            await _grainFactory
                .GetRoomDirectoryGrain()
                .RemoveActiveRoomAsync(_state.RoomId, _state.IsListingChanged, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to remove room {RoomId} from the directory on deactivation",
                _state.RoomId
            );
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
        await PetModule.EnsurePetsLoadedAsync(ct);
        await BotModule.EnsureBotsLoadedAsync(ct);
        await SecurityModule.EnsureRightsLoadedAsync(ct);
        await ModerationModule.EnsureMutesLoadedAsync(ct);
        await EntryModule.EnsureBansLoadedAsync(ct);
        await ModerationModule.EnsureFilterLoadedAsync(ct);
    }

    public Task<RoomSnapshot> GetSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(_state.RoomSnapshot);

    public async Task<RoomSummarySnapshot> GetSummaryAsync(CancellationToken ct)
    {
        var population = await GetRoomPopulationAsync(ct);

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

    public async Task<int> GetRoomPopulationAsync(CancellationToken ct) =>
        await _grainFactory.GetRoomDirectoryGrain().GetRoomPopulationAsync(_state.RoomId, ct);

    public Task<ImmutableArray<KeyValuePair<RoomPropertyType, string>>> GetRoomPropertiesAsync(
        CancellationToken ct
    ) => Task.FromResult(_state.RoomProperties.ToImmutableArray());

    public Task PublishRoomEventAsync(RoomEvent evt, CancellationToken ct) =>
        EventModule.PublishAsync(evt, ct);

    public Task SendComposerToRoomAsync(IComposer composer, CancellationToken ct) =>
        _roomOutbound.OnNextAsync(
            new RoomOutboundSnapshot { RoomId = _state.RoomId, Composer = composer }
        );

    private async Task HydrateRoomStateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        try
        {
            var entity =
                await dbCtx
                    .Rooms.AsNoTracking()
                    .SingleOrDefaultAsync(e => e.Id == (int)_state.RoomId.Value, ct)
                ?? throw new TurboException(TurboErrorCodeEnum.RoomNotFound);

            _state.Model = _roomModelProvider.GetModelById(entity.RoomModelEntityId);

            var ownerName =
                await dbCtx
                    .Players.AsNoTracking()
                    .Where(x => x.Id == entity.PlayerEntityId)
                    .Select(x => x.Name)
                    .FirstOrDefaultAsync(ct)
                ?? string.Empty;

            var raterIds = await dbCtx
                .RoomRatings.AsNoTracking()
                .Where(x => x.RoomEntityId == entity.Id)
                .Select(x => x.PlayerEntityId)
                .ToListAsync(ct);

            _state.PlayerIdsWhoRated.Clear();
            _state.PlayerIdsWhoRated.UnionWith(raterIds.Select(PlayerId.Parse));

            var now = DateTime.UtcNow;
            var eventEntity = await dbCtx
                .RoomEvents.AsNoTracking()
                .Where(x => x.RoomEntityId == entity.Id && x.ExpiresAt > now)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct);

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

            _state.RoomSnapshot = entity.ToSnapshot(
                ownerName,
                _state.Model.Name,
                eventEntity,
                DateTime.UtcNow
            );

            // Before anything can ask for the snapshot: the group is not on the room row, and
            // resolving it lazily meant the first caller to want the room's listing got one
            // with no group in it.
            await GetGuildAsync(ct);

            await SecurityModule.EnsureRightsLoadedAsync(ct);
            await EntryModule.EnsureBansLoadedAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate room {RoomId}", _state.RoomId);

            throw;
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
