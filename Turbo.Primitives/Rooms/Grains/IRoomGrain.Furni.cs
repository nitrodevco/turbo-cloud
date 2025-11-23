using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task MarkItemAsDirtyAsync(long itemId);
}
