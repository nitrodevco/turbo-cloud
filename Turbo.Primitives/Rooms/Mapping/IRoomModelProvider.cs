using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Snapshots.Rooms.Mapping;

namespace Turbo.Primitives.Rooms.Mapping;

public interface IRoomModelProvider
{
    public RoomModelsSnapshot Current { get; }
    public Task ReloadAsync(CancellationToken ct = default);
}
