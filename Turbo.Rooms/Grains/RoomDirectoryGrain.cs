using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Grains.Room;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Rooms.Grains;

[KeepAlive]
public class RoomDirectoryGrain : Grain, IRoomDirectoryGrain
{
    public const string SINGLETON_KEY = "room-directory";

    private readonly Dictionary<long, RoomActiveSnapshot> _activeRooms = [];
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

    public async Task UpdatePopulationAsync(long roomId, int population) =>
        _roomPopulations[roomId] = population;

    public async Task RemoveActiveRoomAsync(long roomId)
    {
        if (!_activeRooms.Remove(roomId))
            return;
    }
}
