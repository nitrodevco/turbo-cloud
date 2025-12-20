using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
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

    private readonly Dictionary<RoomId, RoomActiveSnapshot> _activeRooms = [];
    private readonly Dictionary<RoomId, List<PlayerId>> _roomPlayers = [];
    private readonly Dictionary<RoomId, int> _roomPopulations = [];

    public override Task OnActivateAsync(CancellationToken ct)
    {
        this.RegisterGrainTimer<object?>(
            async _ => await CheckRoomsAsync(ct),
            null,
            TimeSpan.FromMilliseconds(_roomConfig.RoomCheckIntervalMilliseconds),
            TimeSpan.FromMilliseconds(_roomConfig.RoomCheckIntervalMilliseconds)
        );

        return Task.CompletedTask;
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
        Task.FromResult(_roomPopulations.TryGetValue(roomId, out var pop) ? pop : 0);

    public Task UpsertActiveRoomAsync(RoomInfoSnapshot snapshot)
    {
        if (snapshot is not null)
        {
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

        return Task.CompletedTask;
    }

    public Task RemoveActiveRoomAsync(RoomId roomId)
    {
        _activeRooms.Remove(roomId);

        return Task.CompletedTask;
    }

    public async Task AddPlayerToRoomAsync(PlayerId playerId, RoomId roomId, CancellationToken ct)
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

        var room = _grainFactory.GetGrain<IRoomGrain>(roomId);

        await room.RemoveAvatarFromPlayerAsync(playerId, ct);
    }

    public async Task SendComposerToRoomAsync(
        IComposer composer,
        RoomId roomId,
        CancellationToken ct
    )
    {
        if (_roomPlayers.TryGetValue(roomId, out var playerIds) && playerIds.Count > 0)
        {
            foreach (var playerId in playerIds)
            {
                var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);

                await playerPresence.SendComposerAsync(composer, ct);
            }
        }
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
            var roomGrain = _grainFactory.GetGrain<IRoomGrain>(room.RoomId);

            if (population > 0)
                roomGrain.DelayRoomDeactivation();

            if (population == 0)
                roomGrain.DeactivateRoom();
        }

        return Task.CompletedTask;
    }
}
