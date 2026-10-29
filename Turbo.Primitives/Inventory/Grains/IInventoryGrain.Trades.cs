using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Trading;

namespace Turbo.Primitives.Inventory.Grains;

public partial interface IInventoryGrain
{
    /// <summary>
    /// Commits both frozen offers atomically through the initiating inventory. The operation
    /// identity is stable across retries; a matching committed receipt answers true without
    /// moving again. Delivery failure does not change this durable outcome.
    /// </summary>
    Task<bool> CommitFurnitureTradeAsync(
        FurnitureTradeRequest request,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Reloads current custody for pending trades and acknowledges only the repaired receipts.
    /// Noninterleaved, and never calls another inventory. Safe to retry after a lost response.
    /// </summary>
    Task ReconcileFurnitureTradesAsync(CancellationToken ct);
}
