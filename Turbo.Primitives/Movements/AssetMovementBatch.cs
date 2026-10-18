using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Movements;

/// <summary>
/// What a consumer is handed in one call: outbox rows in id order, the range it lost to pruning
/// first when there is one, and the cursor to store with what it writes from them.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetMovementBatch
{
    [Id(0)]
    public required ImmutableArray<OutboxMovement> Movements { get; init; }

    /// <summary>Ids pruned before this consumer read them; null when nothing was lost.</summary>
    [Id(1)]
    public OutboxGap? Gap { get; init; }

    /// <summary>
    /// The cursor to store once the batch is handled. A late batch (rows that committed after the
    /// cursor had passed their ids) leaves it where it was.
    /// </summary>
    [Id(2)]
    public required long Cursor { get; init; }

    /// <summary>
    /// True for rows found below the cursor: a save that committed after rows written later had
    /// been delivered. The consumer may have seen some of them already.
    /// </summary>
    [Id(3)]
    public bool IsLate { get; init; }
}
