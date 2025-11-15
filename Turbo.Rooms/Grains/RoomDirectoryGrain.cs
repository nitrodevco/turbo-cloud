using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Turbo.Contracts.Orleans;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.States.Rooms;

namespace Turbo.Rooms.Grains;

public class RoomDirectoryGrain(
    [PersistentState(OrleansStateNames.ROOM_DIRECTORY, OrleansStorageNames.ROOM_STORE)]
        IPersistentState<RoomDirectoryState> state
) : Grain, IRoomDirectoryGrain
{
    private readonly Dictionary<long, RoomActiveInfoSnapshot> _activeRooms = [];

    public Task<ImmutableArray<RoomActiveInfoSnapshot>> GetActiveRoomsAsync() =>
        Task.FromResult(state.State.ActiveRooms.Values.ToImmutableArray());

    public async Task UpsertActiveRoomAsync(RoomActiveInfoSnapshot info)
    {
        var updated = info with { LastUpdatedUtc = DateTime.UtcNow };
        var activeRooms = state.State.ActiveRooms.ToDictionary();

        activeRooms[updated.RoomId] = updated;

        state.State.ActiveRooms = activeRooms;

        await state.WriteStateAsync();
    }

    public async Task UpdatePopulationAsync(long roomId, int population)
    {
        var activeRooms = state.State.ActiveRooms.ToDictionary();

        if (activeRooms.TryGetValue(roomId, out var existing))
        {
            if (population <= 0)
            {
                activeRooms.Remove(roomId);
            }
            else
            {
                activeRooms[roomId] = existing with
                {
                    Population = population,
                    LastUpdatedUtc = DateTime.UtcNow,
                };
            }
        }

        state.State.ActiveRooms = activeRooms;

        await state.WriteStateAsync();
    }

    public async Task MarkInactiveAsync(long roomId)
    {
        var activeRooms = state.State.ActiveRooms.ToDictionary();

        if (!activeRooms.Remove(roomId))
            return;

        state.State.ActiveRooms = activeRooms;

        await state.WriteStateAsync();
    }
}
