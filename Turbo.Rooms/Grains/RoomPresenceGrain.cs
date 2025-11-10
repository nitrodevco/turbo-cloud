using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Turbo.Contracts.Orleans;
using Turbo.Primitives.Grains;
using Turbo.Primitives.Orleans.States.Rooms;

namespace Turbo.Rooms.Grains;

public class RoomPresenceGrain(
    [PersistentState(OrleansStateNames.ROOM_PRESENCE, OrleansStorageNames.PRESENCE_STORE)]
        IPersistentState<RoomPresenceState> state
) : Grain, IRoomPresenceGrain
{
    public Task<IReadOnlyCollection<long>> GetPlayerIdsAsync() =>
        Task.FromResult((IReadOnlyCollection<long>)state.State.PlayerIds);

    public async Task<bool> AddPlayerIdAsync(long playerId)
    {
        var added = state.State.PlayerIds.Add(playerId);

        if (added)
            await state.WriteStateAsync();

        return added;
    }

    public async Task<bool> RemovePlayerIdAsync(long playerId)
    {
        var removed = state.State.PlayerIds.Remove(playerId);

        if (removed)
            await state.WriteStateAsync();

        return removed;
    }

    public async Task<bool> AddSessionIdAsync(string sessionId)
    {
        var added = state.State.SessionIds.Add(sessionId);

        if (added)
            await state.WriteStateAsync();

        return added;
    }

    public async Task<bool> RemoveSessionIdAsync(string sessionId)
    {
        var removed = state.State.SessionIds.Remove(sessionId);

        if (removed)
            await state.WriteStateAsync();

        return removed;
    }
}
