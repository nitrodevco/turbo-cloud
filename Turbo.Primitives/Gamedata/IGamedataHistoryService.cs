using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// Every change made to the hotel's gamedata, newest first, and rolling one back. A rollback is a
/// change set of its own, so it can be rolled back in turn.
/// </summary>
public interface IGamedataHistoryService
{
    public Task<ImmutableArray<GamedataChangeSetSnapshot>> ListAsync(
        int page,
        CancellationToken ct
    );

    /// <summary>The set's changes; null when there is no such set.</summary>
    public Task<ImmutableArray<GamedataChangeSnapshot>?> GetChangesAsync(
        int changeSetId,
        CancellationToken ct
    );

    /// <summary>
    /// Undoes the set: each field it changed goes back to its earlier value, unless it has
    /// changed again since, and each row it made is removed, unless something uses it now. Null
    /// when there is no such set; throws <see cref="System.InvalidOperationException"/> when it
    /// has been rolled back already.
    /// </summary>
    public Task<GamedataRollbackResult?> RollbackAsync(
        int changeSetId,
        PlayerId player,
        CancellationToken ct
    );
}
