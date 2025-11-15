using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Turbo.Contracts.Orleans;
using Turbo.Primitives.Orleans.Grains.Room;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.States.Room;

namespace Turbo.Rooms.Grains;

public class RoomDirectoryGrain(
    [PersistentState(OrleansStateNames.ROOM_DIRECTORY, OrleansStorageNames.ROOM_STORE)]
        IPersistentState<RoomDirectoryState> state
) : Grain, IRoomDirectoryGrain
{
    public Task<ImmutableArray<RoomSummarySnapshot>> GetActiveRoomsAsync() =>
        Task.FromResult(
            state
                .State.ActiveRooms.Select(x =>
                {
                    var population = state.State.RoomPopulations[x.Value.RoomId];

                    return new RoomSummarySnapshot
                    {
                        RoomId = x.Value.RoomId,
                        Population = population,
                        Name = x.Value.Name,
                        Description = x.Value.Description,
                        OwnerId = x.Value.OwnerId,
                        OwnerName = x.Value.OwnerName,
                        LastUpdatedUtc = x.Value.LastUpdatedUtc,
                    };
                })
                .ToImmutableArray()
        );

    public Task<int> GetRoomPopulationAsync(long roomId) =>
        Task.FromResult(
            state.State.RoomPopulations.TryGetValue(roomId, out var population) ? population : 0
        );

    public async Task UpsertActiveRoomAsync(RoomInfoSnapshot snapshot)
    {
        if (snapshot is null)
            return;

        state.State.ActiveRooms[snapshot.RoomId] = new RoomActiveInfoState
        {
            RoomId = snapshot.RoomId,
            Name = snapshot.RoomName,
            Description = snapshot.Description,
            OwnerId = snapshot.OwnerId,
            OwnerName = snapshot.OwnerName,
            LastUpdatedUtc = DateTime.UtcNow,
        };

        state.State.LastUpdatedUtc = DateTime.UtcNow;

        await state.WriteStateAsync();

        Console.WriteLine($"RoomDirectory - Upserted active room {snapshot.RoomId}");
    }

    public async Task UpdatePopulationAsync(long roomId, int population)
    {
        state.State.RoomPopulations[roomId] = population;

        await state.WriteStateAsync();

        Console.WriteLine($"RoomDirectory - Updated room {roomId} population to {population}");
    }

    public async Task RemoveActiveRoomAsync(long roomId)
    {
        if (!state.State.ActiveRooms.Remove(roomId))
            return;

        state.State.LastUpdatedUtc = DateTime.UtcNow;

        await state.WriteStateAsync();
    }
}
