using System.Collections.Immutable;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Primitives.Orleans.Grains.Room;

public interface IRoomPresenceGrain : IGrainWithIntegerKey
{
    public Task<ImmutableArray<long>> GetPlayerIdsAsync();
    public Task AddPlayerIdAsync(long playerId);
    public Task RemovePlayerIdAsync(long playerId);
}
