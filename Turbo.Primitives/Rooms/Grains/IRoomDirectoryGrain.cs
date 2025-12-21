using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Grains;

public interface IRoomDirectoryGrain : IGrainWithStringKey
{
    public Task<ImmutableArray<RoomSummarySnapshot>> GetActiveRoomsAsync();
    public Task<int> GetRoomPopulationAsync(RoomId roomId);
    public Task UpsertActiveRoomAsync(RoomInfoSnapshot snapshot);
    public Task RemoveActiveRoomAsync(RoomId roomId);
    public Task AddPlayerToRoomAsync(PlayerId playerId, RoomId roomId, CancellationToken ct);
    public Task RemovePlayerFromRoomAsync(PlayerId playerId, RoomId roomId, CancellationToken ct);
}
