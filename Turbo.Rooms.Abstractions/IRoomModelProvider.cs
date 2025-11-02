using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Snapshots.Rooms;

namespace Turbo.Rooms.Abstractions;

public interface IRoomModelProvider
{
    public RoomModelsSnapshot Current { get; }
    public Task ReloadAsync(CancellationToken ct = default);
}
