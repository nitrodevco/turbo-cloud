using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Primitives.Rooms.Grains;

public interface IRoomDirectoryGrain : IGrainWithStringKey
{
    public Task<ImmutableArray<RoomSummarySnapshot>> GetActiveRoomsAsync();
    public Task<int> GetRoomPopulationAsync(RoomId roomId);
    public Task UpsertActiveRoomAsync(RoomInfoSnapshot snapshot);
    public Task RemoveActiveRoomAsync(RoomId roomId);
    public Task AddPlayerToRoomAsync(long playerId, RoomId roomId, CancellationToken ct);
    public Task RemovePlayerFromRoomAsync(long playerId, RoomId roomId, CancellationToken ct);
    public Task SendComposerToRoomAsync(IComposer composer, RoomId roomId, CancellationToken ct);
}
