using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Rooms.Events;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain : IGrainWithIntegerKey
{
    public void DeactivateRoom();
    public void DelayRoomDeactivation();
    public Task EnsureRoomActiveAsync(CancellationToken ct);
    public Task<RoomSnapshot> GetSnapshotAsync();
    public Task<RoomSummarySnapshot> GetSummaryAsync();
    public Task<int> GetRoomPopulationAsync();
    public Task PublishRoomEventAsync(RoomEvent @event, CancellationToken ct);
    public Task SendComposerToRoomAsync(IComposer composer, CancellationToken ct);
}
