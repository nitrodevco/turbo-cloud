using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Snapshots.Rooms;
using Turbo.Primitives.Snapshots.Rooms.Mapping;

namespace Turbo.Rooms.Abstractions;

public interface IRoomModelProvider
{
    public RoomModelsSnapshot Current { get; }
    public Task ReloadAsync(CancellationToken ct = default);
}
