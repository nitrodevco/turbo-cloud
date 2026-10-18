using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Movements;

/// <summary>
/// A plugin that reads the asset movement outbox, registered with
/// <see cref="IAssetMovementConsumerRegistry"/>. Delivery is at least once: a batch that throws
/// is delivered again, and two dispatchers may briefly deliver the same rows. The consumer keeps
/// its own cursor in its own transaction, together with what it wrote from the batch, and drops a
/// movement whose (operation, seq) it has already seen, so its effect is exactly once. See
/// <c>docs/movements.md</c>.
/// </summary>
public interface IAssetMovementConsumer
{
    /// <summary>Names the consumer in logs; one registration per name at a time.</summary>
    string Name { get; }

    /// <summary>The cursor the consumer stored: the outbox id it has handled up to, 0 for none.</summary>
    Task<long> GetCursorAsync(CancellationToken ct);

    /// <summary>
    /// Handles <paramref name="batch"/> and stores <see cref="AssetMovementBatch.Cursor"/>, or keeps
    /// the cursor it has when that is higher (a second dispatcher can be behind), in the same
    /// transaction as what it wrote. Throwing leaves the cursor where it was: the dispatcher asks
    /// for it again and delivers the same rows on its next poll.
    /// </summary>
    Task HandleAsync(AssetMovementBatch batch, CancellationToken ct);
}
