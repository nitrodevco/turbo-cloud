using System.Collections.Immutable;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Primitives.Orleans.Grains.Room;

public interface IRoomDirectoryGrain : IGrainWithIntegerKey
{
    public Task<ImmutableArray<RoomSummarySnapshot>> GetActiveRoomsAsync();
    public Task<int> GetRoomPopulationAsync(long roomId);
    public Task UpsertActiveRoomAsync(RoomInfoSnapshot snapshot);
    public Task UpdatePopulationAsync(long roomId, int population);
    public Task RemoveActiveRoomAsync(long roomId);
}
