using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    public Task OpenFurnitureInventoryAsync(CancellationToken ct);
    public Task OnFurnitureAddedAsync(FurnitureItemSnapshot snapshot, CancellationToken ct);
    public Task OnFurnitureRemovedAsync(RoomObjectId itemId, CancellationToken ct);
}
