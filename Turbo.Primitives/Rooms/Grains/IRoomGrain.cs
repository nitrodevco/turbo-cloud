using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain : IGrainWithIntegerKey
{
    public void DeactivateRoom();
    public void DelayRoomDeactivation();
    public Task EnsureRoomActiveAsync(CancellationToken ct);
    public Task<RoomSnapshot> GetSnapshotAsync(CancellationToken ct);
    public Task<RoomSummarySnapshot> GetSummaryAsync(CancellationToken ct);
    public Task<bool> GetIsGroupRoomAsync(CancellationToken ct);
    public Task<int> GetRoomPopulationAsync(CancellationToken ct);
    public Task PublishRoomEventAsync(RoomEvent evt, CancellationToken ct);
    public Task SendComposerToRoomAsync(IComposer composer, CancellationToken ct);

    /// <summary>
    /// For the server stopping: stops the room, writes everything it changed and unloads it,
    /// while the silo can still reach its persistence grain. Once the silo itself stops, that
    /// call fails and whatever changed since the last hand-over is lost. False when some of it
    /// could not be written.
    /// </summary>
    public Task<bool> SaveAndUnloadAsync(CancellationToken ct);
}
