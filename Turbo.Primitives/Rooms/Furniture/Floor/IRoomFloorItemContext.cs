using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Furniture.Floor;

public interface IRoomFloorItemContext : IRoomItemContext
{
    public IRoomFloorItem Item { get; }
    public Task RefreshItemAsync(CancellationToken ct);
    public Task RefreshStuffDataAsync(CancellationToken ct);
}
