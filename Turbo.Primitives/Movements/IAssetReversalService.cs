using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Movements;

/// <summary>
/// Moves an asset back, for a plugin that undoes movements: only if it is still where the caller
/// believes it is (compare and set), once per operation id, for an actor holding
/// <c>items.reverse</c>, and through the grain that owns the asset, so the move is a movement like
/// any other (cause <see cref="MovementCauses.REVERSAL"/>, cause ref the operation undone). It
/// decides nothing about what to undo. See <c>docs/movements.md</c> "Reversing a movement".
/// </summary>
public interface IAssetReversalService
{
    /// <summary>
    /// Throws when the request's operation id, reason or reversed operation is blank or longer
    /// than its column; every other refusal is an outcome.
    /// </summary>
    Task<ReversalResult> MoveAsync(ReversalRequest request, CancellationToken ct);
}
