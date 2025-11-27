using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Actor;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct);
    public Task<bool> MoveFloorItemByIdAsync(
        ActionContext ctx,
        long itemId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    );
    public Task<bool> RemoveFloorItemByIdAsync(
        ActionContext ctx,
        long itemId,
        CancellationToken ct
    );
    public Task<bool> UseFloorItemByIdAsync(
        ActionContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    );
    public Task<bool> ClickFloorItemByIdAsync(
        ActionContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    );
    public Task<RoomFloorItemSnapshot?> GetFloorItemSnapshotByIdAsync(
        long itemId,
        CancellationToken ct
    );
}
