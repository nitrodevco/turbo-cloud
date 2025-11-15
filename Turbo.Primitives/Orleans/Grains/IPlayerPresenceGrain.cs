using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Primitives.Orleans.Grains;

public interface IPlayerPresenceGrain : IGrainWithIntegerKey
{
    public Task<SessionKey> GetSessionKeyAsync();
    public Task<RoomPointerSnapshot> GetActiveRoomAsync();
    public Task<RoomPendingSnapshot> GetPendingRoomAsync();
    public Task RegisterSessionAsync(SessionKey key, ISessionContextObserver observer);
    public Task UnregisterSessionAsync(SessionKey key);
    public Task SetActiveRoomAsync(long roomId);
    public Task ClearActiveRoomAsync();
    public Task LeaveRoomAsync(long roomId);
    public Task SetPendingRoomAsync(long roomId, bool approved);
    public Task SendComposerAsync(IComposer composer, CancellationToken ct = default);
}
