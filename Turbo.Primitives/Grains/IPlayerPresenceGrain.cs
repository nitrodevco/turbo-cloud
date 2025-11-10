using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Grains.Observers;
using Turbo.Primitives.Orleans.Snapshots.Rooms;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Primitives.Grains;

public interface IPlayerPresenceGrain : IGrainWithIntegerKey
{
    public Task<SessionKey> GetSessionKeyAsync();
    public Task RegisterAsync(SessionKey key, ISessionContextObserver observer);
    public Task UnregisterAsync(SessionKey key);
    public Task<RoomPointerSnapshot> GetActiveRoomAsync();
    public Task<RoomChangedSnapshot> SetActiveRoomAsync(long roomId);
    public Task<RoomChangedSnapshot> ClearActiveRoomAsync();
    public Task<PendingRoomInfoSnapshot> GetPendingRoomAsync();
    public Task SetPendingRoomAsync(long roomId, bool approved);
    public Task ResetAsync();
    public Task SendComposerAsync(IComposer composer, CancellationToken ct = default);
}
