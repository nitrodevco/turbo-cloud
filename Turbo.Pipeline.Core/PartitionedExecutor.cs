using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Core;

public class PartitionedExecutor
{
    private readonly ConcurrentDictionary<string, Mailbox> _boxes = new();

    public Task Enqueue(string key, Func<Task> job, CancellationToken ct)
    {
        var box = _boxes.GetOrAdd(key, _ => new Mailbox());
        return box.Queue.Writer.WriteAsync(job, ct).AsTask();
    }
}
