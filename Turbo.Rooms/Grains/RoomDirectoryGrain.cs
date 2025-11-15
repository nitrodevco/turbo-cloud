using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Turbo.Primitives.Orleans.Grains.Room;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.States.Room;

namespace Turbo.Rooms.Grains;

[KeepAlive]
[Reentrant]
public class RoomDirectoryGrain : Grain, IRoomDirectoryGrain
{
    private readonly Dictionary<long, RoomActiveInfoState> _activeRooms = [];
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
                        Population = population,
                        Name = x.Name,
                        Description = x.Description,
                        OwnerId = x.OwnerId,
                        OwnerName = x.OwnerName,
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

        _activeRooms[snapshot.RoomId] = new RoomActiveInfoState
        {
            RoomId = snapshot.RoomId,
            Name = snapshot.RoomName,
            Description = snapshot.Description,
            OwnerId = snapshot.OwnerId,
            OwnerName = snapshot.OwnerName,
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
