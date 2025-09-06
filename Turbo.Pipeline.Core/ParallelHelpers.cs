using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Core;

public static class ParallelHelpers
{
    public static async Task RunBoundedAsync<T>(
        IEnumerable<T> items,
        int dop,
        Func<T, Task> work,
        CancellationToken ct
    )
    {
        using var sem = new SemaphoreSlim(dop);
        var tasks = new List<Task>();
        foreach (var item in items)
        {
            await sem.WaitAsync(ct).ConfigureAwait(false);
            tasks.Add(
                Task.Run(
                    async () =>
                    {
                        try
                        {
                            await work(item).ConfigureAwait(false);
                        }
                        finally
                        {
                            sem.Release();
                        }
                    },
                    ct
                )
            );
        }
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
