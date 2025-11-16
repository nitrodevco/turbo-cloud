using System.Collections.Immutable;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Primitives.Orleans.Grains.Room;

public interface IRoomGrain : IGrainWithIntegerKey
{
    public Task AddPlayerIdAsync(long playerId);
    public Task RemovePlayerIdAsync(long playerId);
    public Task<ImmutableArray<long>> GetPlayerIdsAsync();
    public Task<RoomSnapshot> GetSnapshotAsync();
    public Task<RoomSummarySnapshot> GetSummaryAsync();
}
