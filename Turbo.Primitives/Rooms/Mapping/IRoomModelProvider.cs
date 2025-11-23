using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Mapping;

public interface IRoomModelProvider
{
    public RoomModelSnapshot GetModelById(int modelId);
    public Task ReloadAsync(CancellationToken ct = default);
}
