using System.Collections.Immutable;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Primitives.Orleans.Grains;

public interface IRoomGrain : IGrainWithIntegerKey
{
    public Task<ImmutableArray<long>> GetPlayerIdsAsync();
    public Task<bool> AddPlayerIdAsync(long playerId);
    public Task<bool> RemovePlayerIdAsync(long playerId);
    public Task<RoomSnapshot> GetSnapshotAsync();
}
