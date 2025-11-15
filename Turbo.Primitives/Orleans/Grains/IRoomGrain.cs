using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;

namespace Turbo.Primitives.Orleans.Grains;

public interface IRoomGrain : IGrainWithIntegerKey
{
    public Task<ImmutableArray<long>> GetPlayerIdsAsync();
    public Task<bool> AddPlayerIdAsync(long playerId);
    public Task<bool> RemovePlayerIdAsync(long playerId);
    public Task<string> GetWorldTypeAsync();
    public Task<RoomSnapshot> GetSnapshotAsync();
    public Task<RoomMapSnapshot> GetMapSnapshotAsync();
    public Task<IReadOnlyList<RoomFloorItemSnapshot>> GetFloorItemSnapshotsAsync();
}
