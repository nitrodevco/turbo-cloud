using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Turbo.Events;

public class PartitionedExecutor
{
    private readonly ConcurrentDictionary<string, Mailbox> _boxes = new();

    public Task Enqueue(string key, Func<Task> work, CancellationToken ct)
    {
        var box = _boxes.GetOrAdd(key, _ => new Mailbox());
        return box.Queue.Writer.WriteAsync(work, ct).AsTask();
    }

    public async Task DrainAsync(TimeSpan timeout)
    {
        // Best-effort: no explicit completion; let the host stop after reasonable delay.
        await Task.WhenAll(_boxes.Values.Select(b => b.PumpTask).ToArray()).WaitAsync(timeout);
    }
}
