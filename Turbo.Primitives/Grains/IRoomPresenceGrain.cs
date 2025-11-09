using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Primitives.Grains;

public interface IRoomPresenceGrain : IGrainWithIntegerKey
{
    public Task<IReadOnlyCollection<long>> GetPlayerIdsAsync();
    public Task<bool> AddPlayerIdAsync(long playerId);
    public Task<bool> RemovePlayerIdAsync(long playerId);
}
