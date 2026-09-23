using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Primitives.Rooms.Providers;

public interface IRoomModelProvider
{
    public RoomModelSnapshot GetModelById(int modelId);
    public Task ReloadAsync(CancellationToken ct = default);

    /// <summary>
    /// Re-reads one model and replaces it in the cache, for a model that changed while the hotel
    /// was running: a room saving its own floor plan. Null when the row is gone.
    /// </summary>
    public Task<RoomModelSnapshot?> ReloadModelAsync(int modelId, CancellationToken ct);
}
