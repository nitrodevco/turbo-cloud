using System.Threading.Tasks;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task MarkItemAsDirtyAsync(long itemId) =>
        Task.FromResult(_liveState.DirtyItemIds.Add(itemId));
}
