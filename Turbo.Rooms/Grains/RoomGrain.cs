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
using Turbo.Catalog.Configuration;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Events;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Providers;
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
    internal readonly WiredChestConfig _wiredChestConfig;
    internal readonly CatalogConfig _catalogConfig;
    internal readonly IGrainFactory _grainFactory;
    internal readonly IRoomModelProvider _roomModelProvider;
    internal readonly IRoomItemsProvider _itemsLoader;
    internal readonly IRoomNpcProvider _npcProvider;
    internal readonly IRoomObjectLogicProvider _logicProvider;
    internal readonly IRoomAvatarProvider _avatarProvider;
    internal readonly IRoomWiredVariablesProvider _wiredVariablesProvider;
    internal readonly IPetBreedProvider _petBreedProvider;
    internal readonly IFurnitureDefinitionProvider _definitionProvider;
    internal readonly IHotelTextProvider _hotelTextProvider;
    internal readonly IChatStyleProvider _chatStyleProvider;
    internal readonly IWordFilter _wordFilter;
    internal readonly ICatalogService _catalogService;
    internal readonly IPermissionRegistryProvider _permissionRegistryProvider;
    internal readonly ICommandRegistryProvider _commandRegistryProvider;
    internal readonly IOperatorCommandRunner _operatorCommandRunner;
    internal readonly IPlayerNoticeService _playerNoticeService;
    internal readonly IRoomEventListenerRegistry _eventListeners;
    internal readonly EventSystem _eventSystem;
    internal readonly ILogger<IRoomGrain> _logger;
    internal readonly IAchievementFactRecorder _achievementFacts;

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
    public readonly RoomCommandSystem CommandSystem;
    public readonly RoomTimerSystem TimerSystem;
    public readonly RoomWaterAreaSystem WaterAreaSystem;
    public readonly RoomWiredChestSystem WiredChestSystem;
    public readonly RoomWiredTransactionSystem WiredTransactionSystem;

    internal IAsyncStream<RoomOutboundSnapshot> _roomOutbound = default!;
    private IGrainTimer? _tickTimer;

    public RoomId RoomId => _state.RoomId;

    public RoomGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<RoomConfig> roomConfig,
        IOptions<PetConfig> petConfig,
        IOptions<BotConfig> botConfig,
        IOptions<WiredConfig> wiredConfig,
        IOptions<WiredChestConfig> wiredChestConfig,
        IOptions<CatalogConfig> catalogConfig,
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
        IChatStyleProvider chatStyleProvider,
        IWordFilter wordFilter,
        ICatalogService catalogService,
        IPermissionRegistryProvider permissionRegistryProvider,
        ICommandRegistryProvider commandRegistryProvider,
        IOperatorCommandRunner operatorCommandRunner,
        IPlayerNoticeService playerNoticeService,
        IRoomEventListenerRegistry eventListeners,
        EventSystem eventSystem,
        IAchievementFactRecorder achievementFacts,
        ILogger<IRoomGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _roomConfig = roomConfig.Value;
        _petConfig = petConfig.Value;
        _botConfig = botConfig.Value;
        _wiredConfig = wiredConfig.Value;
        _wiredChestConfig = wiredChestConfig.Value;
        _catalogConfig = catalogConfig.Value;
        _grainFactory = grainFactory;
        _roomModelProvider = roomModelProvider;
        _itemsLoader = itemsLoader;
        _npcProvider = npcProvider;
        _logicProvider = logicProvider;
        _avatarProvider = avatarProvider;
        _wiredVariablesProvider = wiredVariablesProvider;
        _petBreedProvider = petBreedProvider;
        _definitionProvider = definitionProvider;
        _hotelTextProvider = hotelTextProvider;
        _chatStyleProvider = chatStyleProvider;
        _wordFilter = wordFilter;
        _catalogService = catalogService;
        _permissionRegistryProvider = permissionRegistryProvider;
        _commandRegistryProvider = commandRegistryProvider;
        _operatorCommandRunner = operatorCommandRunner;
        _playerNoticeService = playerNoticeService;
        _eventListeners = eventListeners;
        _eventSystem = eventSystem;
        _logger = logger;
        _achievementFacts = achievementFacts;

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
        CommandSystem = new(this);
        TimerSystem = new(this);
        WaterAreaSystem = new(this);
        WiredChestSystem = new(this);
        WiredTransactionSystem = new(this);

        EventModule.Register(WaterAreaSystem);
        EventModule.Register(RollerSystem);
        EventModule.Register(WiredSystem);
        EventModule.Register(GameSystem);
        EventModule.Register(VariableFxSystem);
        FurniModule.RegisterPlacementLimit(WiredSystem);
        FurniModule.RegisterPlacementLimit(VariableFxSystem);
        EventModule.Register(ChatSystem);
        EventModule.Register(WiredChestSystem);
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
            _state.NextPersistenceBoundaryMs = AlignToNextBoundary(
                now,
                _roomConfig.DirtyItemsTickMs
            );
        }

        await RoomTelemetry.MeasureAsync(
            RoomTelemetry.HYDRATE,
            _state.RoomId,
            () => HydrateRoomStateAsync(ct)
        );

        await _grainFactory.GetRoomDirectoryGrain().UpsertActiveRoomAsync(_state.RoomSnapshot, ct);

        var provider = this.GetStreamProvider(OrleansStreamProviders.ROOM_STREAM_PROVIDER);

        var streamId = StreamId.Create(OrleansStreamNames.ROOM_STREAM, _state.RoomId.Value);

        _roomOutbound = provider.GetStream<RoomOutboundSnapshot>(streamId);

        // One-shot timer re-armed to the next epoch-aligned boundary after each tick.
        // A periodic grain timer measures its period from the end of the previous callback,
        // so tick phase would drift by the callback's execution time and the avatar/wired/roller
        // boundaries (all multiples of RoomTickMs from EpochMs) would be crossed late by a
        // varying amount each cycle.
        _tickTimer = this.RegisterGrainTimer<object?>(
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
            var dormant = IsDormant();

            if (!dormant)
            {
                // Pets and bots decide on the avatar boundary only, just before avatars step, so
                // a walk they pick starts this tick. A walk advances only on that boundary, and
                // deciding on every tick in between re-planned the same walk ten times over.
                if (now >= _state.NextAvatarBoundaryMs)
                {
                    await PetTickSystem.ProcessPetsAsync(now, ct);
                    await BotTickSystem.ProcessBotsAsync(now, ct);
                }

                await AvatarTickSystem.ProcessAvatarsAsync(now, ct);
            }

            await WiredSystem.ProcessWiredAsync(now, dormant, ct);

            if (!dormant)
            {
                await VariableFxSystem.ProcessAsync(now, ct);
                await RollerSystem.ProcessRollersAsync(now, ct);
            }

            await TimerSystem.ProcessTimersAsync(now, ct);
            await FlushDirtyTilesAsync(ct);
            await HandOverToPersistenceIfDueAsync(now, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Room {RoomId} failed to process a tick", _state.RoomId);
        }
        finally
        {
            // Always re-arm: a failed tick must not stop the room.
            RearmTickTimer();
        }
    }

    /// <summary>
    /// A room nobody is in, where no pet or bot is still on its way anywhere, has nothing to
    /// show: it stays loaded until the directory unloads it, and for that while it skips the
    /// pets, bots, avatars, rollers, variable fx and wired's periodic triggers and ticks at the
    /// avatar pace. Wired events, scheduled wired actions, item timers and persistence still
    /// run, because they change what is saved. A player walking in wakes it
    /// (<see cref="WakeTick"/>).
    /// </summary>
    private bool IsDormant() =>
        _state.AvatarsByPlayerId.Count == 0 && !PetModule.AnyWalking() && !BotModule.AnyWalking();

    private void RearmTickTimer()
    {
        var now = NowMs();
        var period = IsDormant() ? _roomConfig.AvatarTickMs : _roomConfig.RoomTickMs;
        var next = AlignToNextBoundary(now, period);

        // AlignToNextBoundary returns `now` when it lands exactly on a boundary; firing again
        // with a zero due time would double-tick the same boundary.
        if (next <= now)
            next = now + period;

        _tickTimer?.Change(TimeSpan.FromMilliseconds(next - now), Timeout.InfiniteTimeSpan);
    }

    /// <summary>Brings a dormant room back to the full tick rate at once, not on its slow beat.</summary>
    internal void WakeTick() => RearmTickTimer();

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _tickTimer?.Dispose();
        _tickTimer = null;

        // Each step is isolated: a failed hand-over must not leave the room listed as active.
        try
        {
            await HandOverToPersistenceAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to hand the changes of room {RoomId} to persistence on deactivation",
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

        await RoomTelemetry.MeasureAsync(
            RoomTelemetry.LOAD_MAP,
            _state.RoomId,
            () => MapModule.EnsureMapBuiltAsync(ct)
        );
        await RoomTelemetry.MeasureAsync(
            RoomTelemetry.LOAD_FURNITURE,
            _state.RoomId,
            () => FurniModule.EnsureFurniLoadedAsync(ct)
        );
        await RoomTelemetry.MeasureAsync(
            RoomTelemetry.LOAD_PETS,
            _state.RoomId,
            () => PetModule.EnsurePetsLoadedAsync(ct)
        );
        await RoomTelemetry.MeasureAsync(
            RoomTelemetry.LOAD_BOTS,
            _state.RoomId,
            () => BotModule.EnsureBotsLoadedAsync(ct)
        );
        await RoomTelemetry.MeasureAsync(
            RoomTelemetry.LOAD_PERMISSIONS,
            _state.RoomId,
            async () =>
            {
                await SecurityModule.EnsureRightsLoadedAsync(ct);
                await ModerationModule.EnsureMutesLoadedAsync(ct);
                await EntryModule.EnsureBansLoadedAsync(ct);
                await ModerationModule.EnsureFilterLoadedAsync(ct);
            }
        );
    }

    public Task<RoomSnapshot> GetSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(_state.RoomSnapshot);

    public Task<RoomSummarySnapshot> GetSummaryAsync(CancellationToken ct) =>
        Task.FromResult(
            new RoomSummarySnapshot
            {
                RoomId = _state.RoomSnapshot.RoomId,
                Name = _state.RoomSnapshot.Name,
                Description = _state.RoomSnapshot.Description,
                OwnerId = _state.RoomSnapshot.OwnerId,
                OwnerName = _state.RoomSnapshot.OwnerName,
                Population = _state.AvatarsByPlayerId.Count,
                LastUpdatedUtc = DateTime.UtcNow,
            }
        );

    /// <summary>
    /// Counted here, where the players are. It used to ask the room directory, a hotel-wide
    /// singleton, for the room's own head count, which it only knows because this room told it.
    /// </summary>
    public Task<int> GetRoomPopulationAsync(CancellationToken ct) =>
        Task.FromResult(_state.AvatarsByPlayerId.Count);

    /// <summary>
    /// The shell a player's request runs in once it reaches this grain: the player counts as
    /// active, and a failure is logged with who, what and on what, then answered with false.
    /// The per-kind copies (bots, pets, avatar actions) were word for word this.
    /// <para>
    /// The service that called the grain logs its own failures only (an inventory it could not
    /// read, a grain call that did not arrive); what failed in here is logged here, once.
    /// </para>
    /// </summary>
    private async Task<bool> RunLoggedAsync(
        ActionContext ctx,
        string action,
        object subject,
        Func<Task<bool>> body
    )
    {
        try
        {
            AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

            return await body();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Player {PlayerId} failed to {Action} {Subject} in room {RoomId}",
                ctx.PlayerId,
                action,
                subject,
                _state.RoomId
            );

            return false;
        }
    }

    public Task PublishRoomEventAsync(RoomEvent evt, CancellationToken ct) =>
        EventModule.PublishAsync(evt, ct);

    public Task SendComposerToRoomAsync(IComposer composer, CancellationToken ct) =>
        _roomOutbound.OnNextAsync(
            new RoomOutboundSnapshot
            {
                RoomId = _state.RoomId,
                Composers = [composer],
                PublishedAtUtcTicks = RoomTelemetry.GetPublicationTimestamp(),
            }
        );

    /// <summary>
    /// <see cref="SendComposerToRoomAsync"/> for several composers that go out together, such as
    /// everything one tick of rollers or wired produced. They are one stream item, so every
    /// player's presence receives them in one call and flushes them to the session in one send,
    /// in this order. An empty batch publishes nothing.
    /// </summary>
    public Task SendComposersToRoomAsync(ImmutableArray<IComposer> composers, CancellationToken ct)
    {
        if (composers.IsDefaultOrEmpty)
            return Task.CompletedTask;

        return _roomOutbound.OnNextAsync(
            new RoomOutboundSnapshot
            {
                RoomId = _state.RoomId,
                Composers = composers,
                PublishedAtUtcTicks = RoomTelemetry.GetPublicationTimestamp(),
            }
        );
    }

    /// <summary>
    /// <see cref="SendComposerToRoomAsync"/> for code that must not wait on it: the tick, and a
    /// broadcast made once the answer is already known. The send goes out and a failure is
    /// logged. The same way to reach the room, not a second one; it exists so the fifteen
    /// callers stop spelling the log line out.
    /// </summary>
    public void SendComposerToRoomAndForget(IComposer composer) =>
        SendComposerToRoomAsync(composer, CancellationToken.None)
            .LogAndForget(_logger, "send a composer to room {RoomId}", _state.RoomId);

    /// <summary>
    /// <see cref="SendComposersToRoomAsync"/> for code that must not wait on it, as
    /// <see cref="SendComposerToRoomAndForget"/> is for one composer.
    /// </summary>
    public void SendComposersToRoomAndForget(ImmutableArray<IComposer> composers) =>
        SendComposersToRoomAsync(composers, CancellationToken.None)
            .LogAndForget(_logger, "send composers to room {RoomId}", _state.RoomId);

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
