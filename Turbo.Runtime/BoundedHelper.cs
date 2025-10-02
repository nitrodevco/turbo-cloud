using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Runtime;

public static class BoundedHelper
{
    public static async Task RunAsync(
        IReadOnlyList<Func<CancellationToken, ValueTask>> work,
        int degree,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(work);

        if (degree <= 0)
            degree = 1;

        using var gate = new SemaphoreSlim(degree);
        var pool = ArrayPool<Task>.Shared;
        var tasks = pool.Rent(work.Count);
        var idx = 0;

        try
        {
            for (int i = 0; i < work.Count; i++)
            {
                await gate.WaitAsync(ct).ConfigureAwait(false);

                var f = work[i];

                tasks[idx++] = RunOneAsync(f, gate, ct);
            }

            await Task.WhenAll(tasks.AsSpan(0, idx).ToArray()).ConfigureAwait(false);
        }
        finally
        {
            pool.Return(tasks, clearArray: true);
        }
    }

    public static Task RunAsync(
        IReadOnlyList<Func<CancellationToken, Task>> work,
        int degree,
        CancellationToken ct
    ) => RunAsync(Convert(work), degree, ct);

    public static Task RunAsync(IEnumerable<Func<Task>> work, int degree, CancellationToken ct)
    {
        var list = work as IList<Func<Task>> ?? [.. work];

        return RunAsync(
            list.Select<Func<Task>, Func<CancellationToken, ValueTask>>(fn =>
                    _ => new ValueTask(fn())
                )
                .ToList(),
            degree,
            ct
        );
    }

    public static Task ForEachAsync<T>(
        IReadOnlyList<T> items,
        int degree,
        Func<T, CancellationToken, ValueTask> body,
        CancellationToken ct
    ) => RunAsync(Project(items, body), degree, ct);

    public static Task ForEachAsync<T>(
        IEnumerable<T> items,
        int degree,
        Func<T, CancellationToken, ValueTask> body,
        CancellationToken ct
    ) => ForEachAsync(items as IReadOnlyList<T> ?? [.. items], degree, body, ct);

    private static IReadOnlyList<Func<CancellationToken, ValueTask>> Convert(
        IReadOnlyList<Func<CancellationToken, Task>> src
    ) =>
        [
            .. src.Select<Func<CancellationToken, Task>, Func<CancellationToken, ValueTask>>(f =>
                ct => new ValueTask(f(ct))
            ),
        ];

    private static IReadOnlyList<Func<CancellationToken, ValueTask>> Project<T>(
        IReadOnlyList<T> items,
        Func<T, CancellationToken, ValueTask> body
    ) => [.. items.Select<T, Func<CancellationToken, ValueTask>>(item => ct => body(item, ct))];

    static async Task RunOneAsync(
        Func<CancellationToken, ValueTask> f,
        SemaphoreSlim gate,
        CancellationToken ct
    )
    {
        try
        {
            await f(ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}
