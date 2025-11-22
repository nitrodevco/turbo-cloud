using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Primitives.Orleans.Grains.Room;

public interface IRoomDirectoryGrain : IGrainWithStringKey
{
    public Task<ImmutableArray<RoomSummarySnapshot>> GetActiveRoomsAsync();
    public Task<int> GetRoomPopulationAsync(long roomId);
    public Task UpsertActiveRoomAsync(RoomInfoSnapshot snapshot);
    public Task RemoveActiveRoomAsync(long roomId);
    public Task AddPlayerToRoomAsync(long playerId, long roomId);
    public Task RemovePlayerFromRoomAsync(long playerId, long roomId);
    public Task SendComposerToRoomAsync(
        IComposer composer,
        long roomId,
        CancellationToken ct = default
    );
}
