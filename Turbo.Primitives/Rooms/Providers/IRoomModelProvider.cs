using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Primitives.Rooms.Providers;

public interface IRoomModelProvider
{
    public RoomModelSnapshot GetModelById(int modelId);
    public Task ReloadAsync(CancellationToken ct = default);
}
