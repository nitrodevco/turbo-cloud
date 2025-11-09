using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Rooms;

namespace Turbo.Primitives.Grains;

public interface IPlayerPresenceGrain : IGrainWithIntegerKey
{
    public Task<RoomPointerSnapshot> GetCurrentRoomAsync();
    public Task<PendingRoomInfoSnapshot> GetPendingRoomAsync();
    public Task ResetAsync();
    public Task SetPendingRoomAsync(long roomId, bool approved);
    public Task<RoomChangedSnapshot> EnterRoomAsync(long roomId);
    public Task<RoomChangedSnapshot> LeaveRoomAsync();
}
