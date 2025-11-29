using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Object.Furniture.Wall;

public interface IRoomWallItemContext : IRoomItemContext
{
    public IRoomWallItem Item { get; }
    public Task AddItemAsync(CancellationToken ct);
    public Task UpdateItemAsync(CancellationToken ct);
    public Task RefreshStuffDataAsync(CancellationToken ct);
    public Task RemoveItemAsync(long pickerId, CancellationToken ct);
}
