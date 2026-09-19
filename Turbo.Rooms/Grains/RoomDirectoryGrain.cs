using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
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
    private readonly ILogger<IRoomDirectoryGrain> _logger;

    private readonly RoomDirectoryLiveState _state = new();

    private IDisposable? _roomCheckTimer;

    public RoomDirectoryGrain(
        IOptions<RoomConfig> roomConfig,
        IGrainFactory grainFactory,
        ILogger<IRoomDirectoryGrain> logger
    )
    {
        _roomConfig = roomConfig.Value;
        _grainFactory = grainFactory;
        _logger = logger;
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

        return Task.CompletedTask;
    }

    public Task RemoveActiveRoomAsync(RoomId roomId, bool listingChanged, CancellationToken ct)
    {
        _state.ActiveRooms.Remove(roomId, out var current);
        _state.ActivatedRooms.Remove(roomId, out var activated);

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
                ActiveRooms = includeActiveRooms
                    ?
                    [
                        .. _state.ActiveRooms.Values.Select(x =>
                            RoomActiveSnapshot.From(
                                x,
                                _state.RoomPopulations.TryGetValue(x.RoomId, out var pop) ? pop : 0
                            )
                        ),
                    ]
                    : [],
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
            playerIds.Add(playerId);

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

        await UpdatePopulationAsync(roomId);
    }

    public Task<int> GetRoomPopulationAsync(RoomId roomId, CancellationToken ct) =>
        Task.FromResult(_state.RoomPopulations.TryGetValue(roomId, out var pop) ? pop : 0);

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

    private Task UpdatePopulationAsync(RoomId roomId)
    {
        _state.RoomPopulations[roomId] = _state.RoomPlayers.TryGetValue(roomId, out var players)
            ? players.Count
            : 0;

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
}
