using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Commands;

public interface ICommandBatchExecutor
{
    /// <summary>Executes distinct players with bounded concurrency, each player's units in order.
    /// False means a confirmed refusal. Exceptions are indeterminate and stop that player's
    /// remaining units; other players continue. Cancellation stops new work. No operation is retried.</summary>
    Task<CommandBatchResult> ExecuteAsync(
        string command,
        IReadOnlyList<ResolvedPlayer> players,
        int unitsPerPlayer,
        Func<ResolvedPlayer, CancellationToken, ValueTask<bool>> operation,
        CancellationToken ct
    );
}
