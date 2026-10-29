using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Trading;

namespace Turbo.Inventory.Grains;

internal sealed partial class InventoryGrain
{
    public Task<bool> CommitFurnitureTradeAsync(
        FurnitureTradeRequest request,
        MovementCause cause,
        CancellationToken ct
    ) => FurniModule.CommitTradeAsync(request, cause, ct);

    public Task ReconcileFurnitureTradesAsync(CancellationToken ct) =>
        FurniModule.EnsureReadyAsync(ct);
}
