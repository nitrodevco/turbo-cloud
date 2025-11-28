using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains;

[KeepAlive]
public class RoomDirectoryGrain(IOptions<RoomConfig> roomConfig, IGrainFactory grainFactory)
    : Grain,
        IRoomDirectoryGrain
{
    public const string SINGLETON_KEY = "room-directory";

    private readonly RoomConfig _roomConfig = roomConfig.Value;
    private readonly IGrainFactory _grainFactory = grainFactory;

    private readonly Dictionary<long, RoomActiveSnapshot> _activeRooms = [];
    private readonly Dictionary<long, List<long>> _roomPlayers = [];
    private readonly Dictionary<long, int> _roomPopulations = [];

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        this.RegisterGrainTimer<object?>(
            async _ => await CheckRoomsAsync(ct),
            null,
            TimeSpan.FromMilliseconds(_roomConfig.RoomCheckIntervalMilliseconds),
            TimeSpan.FromMilliseconds(_roomConfig.RoomCheckIntervalMilliseconds)
        );
    }

    public Task<ImmutableArray<RoomSummarySnapshot>> GetActiveRoomsAsync() =>
        Task.FromResult(
            _activeRooms
                .Values.Select(x =>
                {
                    var population = _roomPopulations.TryGetValue(x.RoomId, out var pop) ? pop : 0;

                    return new RoomSummarySnapshot
                    {
                        RoomId = x.RoomId,
                        Name = x.Name,
                        Description = x.Description,
                        OwnerId = x.OwnerId,
                        OwnerName = x.OwnerName,
                        Population = population,
                        LastUpdatedUtc = x.LastUpdatedUtc,
                    };
                })
                .ToImmutableArray()
        );

    public Task<int> GetRoomPopulationAsync(RoomId roomId) =>
        Task.FromResult(_roomPopulations.TryGetValue(roomId.Value, out var pop) ? pop : 0);

    public async Task UpsertActiveRoomAsync(RoomInfoSnapshot snapshot)
    {
        if (snapshot is null)
            return;

        _activeRooms[snapshot.RoomId] = new RoomActiveSnapshot
        {
            RoomId = snapshot.RoomId,
            Name = snapshot.Name,
            Description = snapshot.Description,
            OwnerId = snapshot.OwnerId,
            OwnerName = snapshot.OwnerName,
            Population = 0,
            LastUpdatedUtc = DateTime.UtcNow,
        };
    }

    public async Task RemoveActiveRoomAsync(RoomId roomId)
    {
        if (!_activeRooms.Remove(roomId.Value))
            return;
    }

    public async Task AddPlayerToRoomAsync(long playerId, RoomId roomId)
    {
        if (!_roomPlayers.TryGetValue(roomId.Value, out var players))
        {
            players = [];
            _roomPlayers[roomId.Value] = players;
        }

        if (!players.Contains(playerId))
            players.Add(playerId);

        await UpdatePopulationAsync(roomId);
    }

    public async Task RemovePlayerFromRoomAsync(long playerId, RoomId roomId)
    {
        if (!_roomPlayers.TryGetValue(roomId.Value, out var players))
            return;

        if (!players.Remove(playerId))
            return;

        await UpdatePopulationAsync(roomId);
    }

    public async Task SendComposerToRoomAsync(
        IComposer composer,
        RoomId roomId,
        CancellationToken ct = default
    )
    {
        if (!_roomPlayers.TryGetValue(roomId.Value, out var playerIds) || playerIds.Count == 0)
            return;

        foreach (var playerId in playerIds)
        {
            var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);

            _ = playerPresence.SendComposerAsync(composer, ct);
        }
    }

    private async Task UpdatePopulationAsync(RoomId roomId) =>
        _roomPopulations[roomId.Value] = _roomPlayers.TryGetValue(roomId.Value, out var players)
            ? players.Count
            : 0;

    private async Task CheckRoomsAsync(CancellationToken ct)
    {
        var rooms = _activeRooms.Values.ToArray();

        foreach (var room in rooms)
        {
            var population = _roomPopulations.TryGetValue(room.RoomId, out var pop) ? pop : 0;
            var roomGrain = _grainFactory.GetGrain<IRoomGrain>(room.RoomId);

            if (population > 0)
                roomGrain.DelayRoomDeactivation();

            if (population == 0)
                roomGrain.DeactivateRoom();
        }
    }
}
