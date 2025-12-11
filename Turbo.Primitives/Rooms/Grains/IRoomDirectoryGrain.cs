using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Primitives.Rooms.Grains;

public interface IRoomDirectoryGrain : IGrainWithStringKey
{
    public Task<ImmutableArray<RoomSummarySnapshot>> GetActiveRoomsAsync();
    public Task<int> GetRoomPopulationAsync(int roomId);
    public Task UpsertActiveRoomAsync(RoomInfoSnapshot snapshot);
    public Task RemoveActiveRoomAsync(int roomId);
    public Task AddPlayerToRoomAsync(long playerId, int roomId, CancellationToken ct);
    public Task RemovePlayerFromRoomAsync(long playerId, int roomId, CancellationToken ct);
    public Task SendComposerToRoomAsync(IComposer composer, int roomId, CancellationToken ct);
}
