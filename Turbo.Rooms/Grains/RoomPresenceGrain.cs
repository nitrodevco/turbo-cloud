using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Turbo.Contracts.Orleans;
using Turbo.Primitives.Orleans.Grains.Room;
using Turbo.Primitives.Orleans.States.Room;

namespace Turbo.Rooms.Grains;

public class RoomPresenceGrain(
    [PersistentState(OrleansStateNames.ROOM_PRESENCE, OrleansStorageNames.ROOM_STORE)]
        IPersistentState<RoomPresenceState> state,
    IGrainFactory grainFactory
) : Grain, IRoomPresenceGrain
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public Task<ImmutableArray<long>> GetPlayerIdsAsync() =>
        Task.FromResult(state.State.PlayerIds.ToImmutableArray());

    public async Task AddPlayerIdAsync(long playerId)
    {
        if (!state.State.PlayerIds.Add(playerId))
            return;

        state.State.LastUpdatedUtc = DateTime.UtcNow;

        await state.WriteStateAsync();

        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(0)
            .UpdatePopulationAsync(this.GetPrimaryKeyLong(), state.State.PlayerIds.Count);

        Console.WriteLine(
            $"Room {this.GetPrimaryKeyLong()} - Added player {playerId}. Population: {state.State.PlayerIds.Count}"
        );
    }

    public async Task RemovePlayerIdAsync(long playerId)
    {
        if (!state.State.PlayerIds.Remove(playerId))
            return;

        state.State.LastUpdatedUtc = DateTime.UtcNow;

        await state.WriteStateAsync();

        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(0)
            .UpdatePopulationAsync(this.GetPrimaryKeyLong(), state.State.PlayerIds.Count);

        Console.WriteLine(
            $"Room {this.GetPrimaryKeyLong()} - Removed player {playerId}. Population: {state.State.PlayerIds.Count}"
        );
    }
}
