using System.Collections.Immutable;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Primitives.Orleans.Grains;

public interface IRoomDirectoryGrain : IGrainWithIntegerKey
{
    public Task<ImmutableArray<RoomActiveInfoSnapshot>> GetActiveRoomsAsync();
    public Task UpsertActiveRoomAsync(RoomActiveInfoSnapshot info);
    public Task UpdatePopulationAsync(long roomId, int population);
    public Task MarkInactiveAsync(long roomId);
}
