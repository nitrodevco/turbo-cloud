using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Orleans.Grains.Room;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Rooms.Grains;

[KeepAlive]
public class RoomDirectoryGrain(IGrainFactory grainFactory) : Grain, IRoomDirectoryGrain
{
    public const string SINGLETON_KEY = "room-directory";

    private readonly IGrainFactory _grainFactory = grainFactory;

    private readonly Dictionary<long, RoomActiveSnapshot> _activeRooms = [];
    private readonly Dictionary<long, List<long>> _roomPlayers = [];
    private readonly Dictionary<long, int> _roomPopulations = [];

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

    public Task<int> GetRoomPopulationAsync(long roomId) =>
        Task.FromResult(_roomPopulations.TryGetValue(roomId, out var pop) ? pop : 0);

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

    public async Task RemoveActiveRoomAsync(long roomId)
    {
        if (!_activeRooms.Remove(roomId))
            return;
    }

    public async Task AddPlayerToRoomAsync(long playerId, long roomId)
    {
        if (!_roomPlayers.TryGetValue(roomId, out var players))
        {
            players = [];
            _roomPlayers[roomId] = players;
        }

        if (!players.Contains(playerId))
            players.Add(playerId);

        await UpdatePopulationAsync(roomId);
    }

    public async Task RemovePlayerFromRoomAsync(long playerId, long roomId)
    {
        if (!_roomPlayers.TryGetValue(roomId, out var players))
            return;

        if (!players.Remove(playerId))
            return;

        await UpdatePopulationAsync(roomId);
    }

    public async Task SendComposerToRoomAsync(
        IComposer composer,
        long roomId,
        CancellationToken ct = default
    )
    {
        if (!_roomPlayers.TryGetValue(roomId, out var playerIds) || playerIds.Count == 0)
            return;

        foreach (var playerId in playerIds)
        {
            var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);

            _ = playerPresence.SendComposerAsync(composer, ct);
        }
    }

    private async Task UpdatePopulationAsync(long roomId) =>
        _roomPopulations[roomId] = _roomPlayers.TryGetValue(roomId, out var players)
            ? players.Count
            : 0;
}
