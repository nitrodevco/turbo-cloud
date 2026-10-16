using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Events;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains;

/// <summary>
/// The rooms that are active right now, their populations and who is in them, one grain for
/// the hotel. Nothing is persisted: the directory is rebuilt as rooms activate and report in,
/// so deactivation only stops the timer.
/// </summary>
[KeepAlive]
internal sealed class RoomDirectoryGrain : Grain, IRoomDirectoryGrain
{
    private readonly RoomConfig _roomConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly EventSystem _eventSystem;
    private readonly ILogger<IRoomDirectoryGrain> _logger;
    private readonly TimeProvider _time;

    private readonly RoomDirectoryLiveState _state = new();

    private IDisposable? _roomCheckTimer;

    public RoomDirectoryGrain(
        IOptions<RoomConfig> roomConfig,
        IGrainFactory grainFactory,
        EventSystem eventSystem,
        ILogger<IRoomDirectoryGrain> logger,
        TimeProvider time
    )
    {
        _roomConfig = roomConfig.Value;
        _grainFactory = grainFactory;
        _eventSystem = eventSystem;
        _logger = logger;
        _time = time;
    }

    public override Task OnActivateAsync(CancellationToken ct)
    {
        _roomCheckTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((RoomDirectoryGrain)self!).CheckRoomsAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_roomConfig.RoomCheckMs),
            TimeSpan.FromMilliseconds(_roomConfig.RoomCheckMs)
        );

        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _roomCheckTimer?.Dispose();
        _roomCheckTimer = null;

        return Task.CompletedTask;
    }

    public Task<ImmutableArray<RoomId>> GetActiveRoomIdsAsync(CancellationToken ct) =>
        Task.FromResult<ImmutableArray<RoomId>>([.. _state.ActiveRooms.Keys]);

    public Task UpsertActiveRoomAsync(RoomInfoSnapshot snapshot, CancellationToken ct)
    {
        if (snapshot is null)
            return Task.CompletedTask;

        // Stored as plain room info so private settings (password, moderation) never leave here.
        var room = RoomActiveSnapshot.From(snapshot, 0) with
        {
            LastUpdatedUtc = DateTime.UtcNow,
        };

        _state.ActiveRooms[snapshot.RoomId] = room;
        _state.ActivatedRooms.TryAdd(snapshot.RoomId, room);
        _state.ActiveRoomsView = null;
        Announce(snapshot.RoomId, null);

        return Task.CompletedTask;
    }

    public Task RemoveActiveRoomAsync(RoomId roomId, bool listingChanged, CancellationToken ct)
    {
        _state.ActiveRooms.Remove(roomId, out var current);
        _state.ActivatedRooms.Remove(roomId, out var activated);
        _state.RoomEntries.Remove(roomId);
        _state.ActiveRoomsView = null;

        if (current is not null)
            Announce(roomId, null);

        if (listingChanged && current is not null)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal)
            {
                NavigatorListingKeys.Room(roomId),
                NavigatorListingKeys.Owner(current.OwnerId),
                NavigatorListingKeys.Category(current.CategoryId),
                NavigatorListingKeys.HIGHEST_SCORED,
                NavigatorListingKeys.STAFF_PICKS,
                NavigatorListingKeys.EVENTS,
                NavigatorListingKeys.SEARCH,
                NavigatorListingKeys.TAGS,
            };

            if (activated is not null)
                keys.Add(NavigatorListingKeys.Category(activated.CategoryId));

            AppendListingChanges(keys);
        }

        return Task.CompletedTask;
    }

    public Task PublishListingChangesAsync(IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        AppendListingChanges(keys);

        return Task.CompletedTask;
    }

    public Task<RoomListingViewSnapshot> GetListingViewAsync(
        Guid epoch,
        long sinceSequence,
        bool includeActiveRooms,
        CancellationToken ct
    )
    {
        var oldestKnown =
            _state.ListingChanges.Count > 0
                ? _state.ListingChanges.Peek().Sequence
                : _state.ListingSequence + 1;

        // A caller from another directory activation, or one older than the log, cannot catch up
        // key by key.
        var isReset =
            epoch != _state.ListingEpoch
            || sinceSequence > _state.ListingSequence
            || sinceSequence < oldestKnown - 1;

        return Task.FromResult(
            new RoomListingViewSnapshot
            {
                ActiveRooms = includeActiveRooms ? GetActiveRoomsView() : [],
                Epoch = _state.ListingEpoch,
                Sequence = _state.ListingSequence,
                ChangedKeys = isReset
                    ? []
                    :
                    [
                        .. _state
                            .ListingChanges.Where(x => x.Sequence > sinceSequence)
                            .Select(x => x.Key)
                            .Distinct(StringComparer.Ordinal),
                    ],
                IsReset = isReset,
            }
        );
    }

    /// <summary>
    /// The active rooms with their populations and recent visitors, built once per change (or
    /// once a counted entry leaves the hot-room window) rather than per request. The array and
    /// its snapshots are immutable, so every caller can share one.
    /// </summary>
    private ImmutableArray<RoomActiveSnapshot> GetActiveRoomsView()
    {
        var now = _time.GetUtcNow().UtcDateTime;

        if (_state.ActiveRoomsView is { } view && now < _state.ActiveRoomsViewExpiresUtc)
            return view;

        var window = TimeSpan.FromSeconds(Math.Max(0, _roomConfig.HotRoomWindowSeconds));
        var expires = DateTime.MaxValue;

        foreach (var (roomId, entries) in _state.RoomEntries.ToArray())
        {
            foreach (var (playerId, enteredAt) in entries.ToArray())
            {
                if (enteredAt + window <= now)
                    entries.Remove(playerId);
                else if (enteredAt + window < expires)
                    expires = enteredAt + window;
            }

            if (entries.Count == 0)
                _state.RoomEntries.Remove(roomId);
        }

        _state.ActiveRoomsViewExpiresUtc = expires;

        ImmutableArray<RoomActiveSnapshot> built =
        [
            .. _state.ActiveRooms.Values.Select(x =>
                RoomActiveSnapshot.From(
                    x,
                    _state.RoomPopulations.TryGetValue(x.RoomId, out var pop) ? pop : 0,
                    _state.RoomEntries.TryGetValue(x.RoomId, out var entries) ? entries.Count : 0
                )
            ),
        ];

        _state.ActiveRoomsView = built;

        return built;
    }

    private void AppendListingChanges(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            _state.ListingChanges.Enqueue((++_state.ListingSequence, key));

            while (_state.ListingChanges.Count > _roomConfig.ListingChangeLogSize)
                _state.ListingChanges.Dequeue();
        }
    }

    public async Task AddPlayerToRoomAsync(PlayerId playerId, RoomId roomId, CancellationToken ct)
    {
        if (!_state.RoomPlayers.TryGetValue(roomId, out var playerIds))
        {
            playerIds = [];
            _state.RoomPlayers[roomId] = playerIds;
        }

        if (!playerIds.Contains(playerId))
        {
            playerIds.Add(playerId);
            RecordEntry(roomId, playerId);
            Announce(roomId, playerId);
        }

        await UpdatePopulationAsync(roomId);
    }

    public async Task RemovePlayerFromRoomAsync(
        PlayerId playerId,
        RoomId roomId,
        CancellationToken ct
    )
    {
        if (!_state.RoomPlayers.TryGetValue(roomId, out var players))
            return;

        if (!players.Remove(playerId))
            return;

        Announce(roomId, playerId);
        await UpdatePopulationAsync(roomId);
    }

    public Task<int> GetRoomPopulationAsync(RoomId roomId, CancellationToken ct) =>
        Task.FromResult(_state.RoomPopulations.TryGetValue(roomId, out var pop) ? pop : 0);

    public Task<ImmutableArray<PlayerId>> GetRoomPlayersAsync(
        RoomId roomId,
        CancellationToken ct
    ) =>
        Task.FromResult<ImmutableArray<PlayerId>>(
            _state.RoomPlayers.TryGetValue(roomId, out var players) ? [.. players] : []
        );

    public Task<RoomId?> GetRandomPopulatedRoomAsync(CancellationToken ct)
    {
        var populated = _state
            .RoomPopulations.Where(kv => kv.Value > 0)
            .Select(kv => kv.Key)
            .ToArray();

        if (populated.Length == 0)
            return Task.FromResult<RoomId?>(null);

        var random = Random.Shared.Next(populated.Length);
        return Task.FromResult<RoomId?>(populated[random]);
    }

    // Official client capture 2026-10-14 (WIN63-202610131318): Find Friends from the player's own
    // room forwarded them into another user's populated room. Leaving out their own rooms and the
    // one they stand in is inference from that: sending them where they are finds nobody new.
    public Task<RoomId?> GetFriendFindingRoomAsync(PlayerId playerId, CancellationToken ct)
    {
        var candidates = _state
            .RoomPopulations.Where(kv =>
                kv.Value > 0
                && !(
                    _state.RoomPlayers.TryGetValue(kv.Key, out var players)
                    && players.Contains(playerId)
                )
                && !(
                    _state.ActiveRooms.TryGetValue(kv.Key, out var room) && room.OwnerId == playerId
                )
            )
            .Select(kv => kv.Key)
            .ToArray();

        return Task.FromResult<RoomId?>(
            candidates.Length == 0 ? null : candidates[Random.Shared.Next(candidates.Length)]
        );
    }

    /// <summary>
    /// Remembers when the player entered, once per player: walking in and out again does not
    /// make a room hotter.
    /// </summary>
    private void RecordEntry(RoomId roomId, PlayerId playerId)
    {
        if (_roomConfig.HotRoomWindowSeconds <= 0)
            return;

        if (!_state.RoomEntries.TryGetValue(roomId, out var entries))
        {
            entries = [];
            _state.RoomEntries[roomId] = entries;
        }

        entries[playerId] = _time.GetUtcNow().UtcDateTime;
    }

    private Task UpdatePopulationAsync(RoomId roomId)
    {
        _state.RoomPopulations[roomId] = _state.RoomPlayers.TryGetValue(roomId, out var players)
            ? players.Count
            : 0;
        _state.ActiveRoomsView = null;

        return Task.CompletedTask;
    }

    private Task CheckRoomsAsync(CancellationToken ct)
    {
        var rooms = _state.ActiveRooms.Values.ToArray();

        foreach (var room in rooms)
        {
            var population = _state.RoomPopulations.TryGetValue(room.RoomId, out var pop) ? pop : 0;
            var roomGrain = _grainFactory.GetRoomGrain(room.RoomId);

            if (population > 0)
                roomGrain.DelayRoomDeactivation();

            if (population == 0)
                roomGrain.DeactivateRoom();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Raises <see cref="RoomActivityChangedEvent"/>. Not awaited: a handler may call back into
    /// this grain, which would wait on the call raising it.
    /// </summary>
    private void Announce(RoomId roomId, PlayerId? playerId) =>
        _eventSystem
            .PublishAsync(
                new RoomActivityChangedEvent { RoomId = roomId, PlayerId = playerId },
                CancellationToken.None
            )
            .LogAndForget(_logger, "announce activity in room {RoomId}", roomId);
}
