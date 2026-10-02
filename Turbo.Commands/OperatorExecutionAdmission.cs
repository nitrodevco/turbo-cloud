using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Turbo.Commands;

/// <summary>Bounded FIFO admission per executor. A lease ends only when its execution has settled.</summary>
internal sealed class OperatorExecutionAdmission
{
    private readonly object _sync = new();
    private readonly Dictionary<int, ExecutorQueue> _queues = [];
    private int _outstanding;

    internal Lease? TryEnter(int executor, int globalLimit, int executorLimit)
    {
        lock (_sync)
        {
            _queues.TryGetValue(executor, out var queue);
            if (_outstanding >= globalLimit || (queue?.Outstanding ?? 0) >= executorLimit)
                return null;

            queue ??= new ExecutorQueue();
            _queues[executor] = queue;
            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            var lease = new Lease(this, executor, queue.Tail, completion);
            queue.Tail = completion.Task;
            queue.Outstanding++;
            _outstanding++;
            return lease;
        }
    }

    private void Exit(int executor, TaskCompletionSource completion)
    {
        lock (_sync)
        {
            var queue = _queues[executor];
            queue.Outstanding--;
            _outstanding--;
            if (queue.Outstanding == 0)
                _queues.Remove(executor);
            completion.TrySetResult();
        }
    }

    private sealed class ExecutorQueue
    {
        internal Task Tail { get; set; } = Task.CompletedTask;
        internal int Outstanding { get; set; }
    }

    internal sealed class Lease(
        OperatorExecutionAdmission owner,
        int executor,
        Task predecessor,
        TaskCompletionSource completion
    ) : IDisposable
    {
        internal Task Predecessor { get; } = predecessor;

        public void Dispose() => owner.Exit(executor, completion);
    }
}
