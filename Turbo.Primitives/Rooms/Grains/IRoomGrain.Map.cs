using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task ClickTileAsync(ActionContext ctx, int x, int y, CancellationToken ct);
    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int x, int y, CancellationToken ct);
    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int id, CancellationToken ct);
    public Task<RoomMapSnapshot> GetMapSnapshotAsync(CancellationToken ct);

    /// <summary>Tiles holding furniture, for the floor plan editor.</summary>
    public Task<ImmutableArray<RoomTilePositionSnapshot>> GetOccupiedTilesAsync(
        CancellationToken ct
    );
}
