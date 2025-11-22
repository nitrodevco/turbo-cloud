using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Furniture.Wall;

public interface IRoomWallItemContext : IRoomItemContext
{
    public IRoomWallItem Item { get; }
    public void MarkItemDirty();
    public Task RefreshItemAsync(CancellationToken ct);
    public Task RefreshStuffDataAsync(CancellationToken ct);
}
