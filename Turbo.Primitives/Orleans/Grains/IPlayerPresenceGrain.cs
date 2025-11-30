using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Session;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Orleans.Grains;

public interface IPlayerPresenceGrain : IGrainWithIntegerKey
{
    public Task<SessionKey> GetSessionKeyAsync();
    public Task<RoomPointerSnapshot> GetActiveRoomAsync();
    public Task<RoomPendingSnapshot> GetPendingRoomAsync();
    public Task RegisterSessionAsync(SessionKey key, ISessionContextObserver observer);
    public Task UnregisterSessionAsync(SessionKey key, CancellationToken ct);
    public Task SetActiveRoomAsync(RoomId roomId, CancellationToken ct);
    public Task ClearActiveRoomAsync(CancellationToken ct);
    public Task LeaveRoomAsync(RoomId roomId, CancellationToken ct);
    public Task SetPendingRoomAsync(RoomId roomId, bool approved);
    public Task SendComposerAsync(IComposer composer, CancellationToken ct);
}
