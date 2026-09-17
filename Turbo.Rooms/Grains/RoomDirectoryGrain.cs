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

[KeepAlive]
internal sealed class RoomDirectoryGrain(
    IOptions<RoomConfig> roomConfig,
    ILogger<IRoomDirectoryGrain> logger,
    IGrainFactory grainFactory
) : Grain, IRoomDirectoryGrain
{
    private readonly RoomConfig _roomConfig = roomConfig.Value;
    private readonly ILogger<IRoomDirectoryGrain> _logger = logger;
    private readonly IGrainFactory _grainFactory = grainFactory;

    private readonly Dictionary<RoomId, RoomInfoSnapshot> _activeRooms = [];

    // How each room looked when it became active, so a listing it has since left (for example
    // an old category) is also invalidated when it deactivates.
    private readonly Dictionary<RoomId, RoomInfoSnapshot> _activatedRooms = [];
    private readonly Queue<(long Sequence, string Key)> _listingChanges = new();
    private readonly Guid _listingEpoch = Guid.NewGuid();
    private long _listingSequence;
    private IDisposable? _roomCheckTimer;
    private readonly Dictionary<RoomId, List<PlayerId>> _roomPlayers = [];
    private readonly Dictionary<RoomId, int> _roomPopulations = [];

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

        _activeRooms[snapshot.RoomId] = room;
        _activatedRooms.TryAdd(snapshot.RoomId, room);

        return Task.CompletedTask;
    }

    public Task RemoveActiveRoomAsync(RoomId roomId, bool listingChanged, CancellationToken ct)
    {
        _activeRooms.Remove(roomId, out var current);
        _activatedRooms.Remove(roomId, out var activated);

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
            _listingChanges.Count > 0 ? _listingChanges.Peek().Sequence : _listingSequence + 1;

        // A caller from another directory activation, or one older than the log, cannot catch up
        // key by key.
        var isReset =
            epoch != _listingEpoch
            || sinceSequence > _listingSequence
            || sinceSequence < oldestKnown - 1;

        return Task.FromResult(
            new RoomListingViewSnapshot
            {
                ActiveRooms = includeActiveRooms
                    ?
                    [
                        .. _activeRooms.Values.Select(x =>
                            RoomActiveSnapshot.From(
                                x,
                                _roomPopulations.TryGetValue(x.RoomId, out var pop) ? pop : 0
                            )
                        ),
                    ]
                    : [],
                Epoch = _listingEpoch,
                Sequence = _listingSequence,
                ChangedKeys = isReset
                    ? []
                    :
                    [
                        .. _listingChanges
                            .Where(x => x.Sequence > sinceSequence)
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
            _listingChanges.Enqueue((++_listingSequence, key));

            while (_listingChanges.Count > _roomConfig.ListingChangeLogSize)
                _listingChanges.Dequeue();
        }
    }

    public async Task AddPlayerToRoomAsync(PlayerId playerId, RoomId roomId, CancellationToken ct)
    {
        if (!_roomPlayers.TryGetValue(roomId, out var playerIds))
        {
            playerIds = [];
            _roomPlayers[roomId] = playerIds;
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
        if (!_roomPlayers.TryGetValue(roomId, out var players))
            return;

        if (!players.Remove(playerId))
            return;

        await UpdatePopulationAsync(roomId);
    }

    public Task<int> GetRoomPopulationAsync(RoomId roomId, CancellationToken ct) =>
        Task.FromResult(_roomPopulations.TryGetValue(roomId, out var pop) ? pop : 0);

    public Task<RoomId?> GetRandomPopulatedRoomAsync(CancellationToken ct)
    {
        var populated = _roomPopulations.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToArray();

        if (populated.Length == 0)
            return Task.FromResult<RoomId?>(null);

        var random = Random.Shared.Next(populated.Length);
        return Task.FromResult<RoomId?>(populated[random]);
    }

    private Task UpdatePopulationAsync(RoomId roomId)
    {
        _roomPopulations[roomId] = _roomPlayers.TryGetValue(roomId, out var players)
            ? players.Count
            : 0;

        return Task.CompletedTask;
    }

    private Task CheckRoomsAsync(CancellationToken ct)
    {
        var rooms = _activeRooms.Values.ToArray();

        foreach (var room in rooms)
        {
            var population = _roomPopulations.TryGetValue(room.RoomId, out var pop) ? pop : 0;
            var roomGrain = _grainFactory.GetRoomGrain(room.RoomId);

            if (population > 0)
                roomGrain.DelayRoomDeactivation();

            if (population == 0)
                roomGrain.DeactivateRoom();
        }

        return Task.CompletedTask;
    }
}
