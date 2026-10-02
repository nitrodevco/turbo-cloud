using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Commands;

namespace Turbo.Commands;

public sealed class CommandBatchExecutor(
    IOptions<CommandConfig> config,
    ILogger<ICommandBatchExecutor> logger
) : ICommandBatchExecutor
{
    private readonly SemaphoreSlim _operations = new(config.Value.MaxBatchConcurrency);

    public async Task<CommandBatchResult> ExecuteAsync(
        string command,
        IReadOnlyList<ResolvedPlayer> players,
        int unitsPerPlayer,
        Func<ResolvedPlayer, CancellationToken, ValueTask<bool>> operation,
        CancellationToken ct
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(unitsPerPlayer, 1);
        var concurrency = config.Value.MaxBatchConcurrency;
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrency, 1);
        var targets = players.DistinctBy(x => x.Id).ToArray();
        var results = targets
            .Select(x => new CommandBatchTargetResult(x.Id, unitsPerPlayer, 0, 0, 0))
            .ToArray();
        var next = -1;

        await Task.WhenAll(
            Enumerable.Range(0, Math.Min(concurrency, targets.Length)).Select(_ => RunWorkerAsync())
        );

        return new CommandBatchResult(Array.AsReadOnly(results), ct.IsCancellationRequested);

        async Task RunWorkerAsync()
        {
            while (!ct.IsCancellationRequested)
            {
                var index = Interlocked.Increment(ref next);
                if (index >= targets.Length)
                    return;

                var player = targets[index];
                var succeeded = 0;
                var failed = 0;
                var indeterminate = 0;

                for (var unit = 0; unit < unitsPerPlayer && !ct.IsCancellationRequested; unit++)
                {
                    var admitted = false;
                    var attempted = false;
                    try
                    {
                        await _operations.WaitAsync(ct);
                        admitted = true;
                        ct.ThrowIfCancellationRequested();
                        attempted = true;
                        if (await operation(player, ct))
                            succeeded++;
                        else
                            failed++;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        if (attempted)
                            indeterminate++;
                        break;
                    }
                    catch (Exception ex)
                    {
                        indeterminate++;
                        logger.LogError(
                            ex,
                            "Command {Command} operation is indeterminate for player {PlayerId}",
                            command,
                            player.Id
                        );
                        break;
                    }
                    finally
                    {
                        if (admitted)
                            _operations.Release();
                    }
                }

                results[index] = new CommandBatchTargetResult(
                    player.Id,
                    unitsPerPlayer,
                    succeeded,
                    failed,
                    indeterminate
                );
            }
        }
    }
}
