using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Rooms.Grains;

public class RoomDirectoryGrain : Grain, IRoomDirectoryGrain
{
    private readonly Dictionary<long, RoomActiveInfoSnapshot> _activeRooms = [];

    public Task<ImmutableArray<RoomActiveInfoSnapshot>> GetActiveRoomsAsync() =>
        Task.FromResult(_activeRooms.Values.ToImmutableArray());

    public Task UpsertActiveRoomAsync(RoomActiveInfoSnapshot info)
    {
        var updated = info with { LastUpdatedUtc = DateTime.UtcNow };

        _activeRooms[info.RoomId] = updated;

        return Task.CompletedTask;
    }

    public Task UpdatePopulationAsync(long roomId, int population)
    {
        if (_activeRooms.TryGetValue(roomId, out var existing))
        {
            if (population <= 0)
            {
                _activeRooms.Remove(roomId);
            }
            else
            {
                _activeRooms[roomId] = existing with
                {
                    Population = population,
                    LastUpdatedUtc = DateTime.UtcNow,
                };
            }
        }

        return Task.CompletedTask;
    }

    public Task MarkInactiveAsync(long roomId)
    {
        _activeRooms.Remove(roomId);

        return Task.CompletedTask;
    }
}
