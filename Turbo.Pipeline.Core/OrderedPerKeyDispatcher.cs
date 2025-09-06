using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Core;

public class OrderedPerKeyDispatcher<TKey, TItem> : IAsyncDisposable
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, PerKeyQueue> _queues = new();

    // Keys that have been explicitly closed (e.g., session destroyed). Prevents resurrection.
    private readonly ConcurrentDictionary<TKey, byte> _closedKeys = new();

    private readonly Func<TItem, TKey> _keySelector;
    private readonly Func<TItem, CancellationToken, Task> _handler;
    private readonly int _capacityPerKey;
    private readonly BoundedChannelFullMode _fullMode;
    private readonly bool _dropIfCanceledBeforeStart;

    private sealed class Work
    {
        public required TItem Item;
        public required TaskCompletionSource Completion;
        public required CancellationToken Ct;
    }

    private sealed class PerKeyQueue : IAsyncDisposable
    {
        public readonly Channel<Work> Queue;
        public Task? Consumer;

        // For hard aborts (optional)
        public readonly CancellationTokenSource LifetimeCts = new();

        public PerKeyQueue(int capacity, BoundedChannelFullMode mode)
        {
            Queue = Channel.CreateBounded<Work>(
                new BoundedChannelOptions(capacity)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = mode,
                }
            );
        }

        public async ValueTask DisposeAsync()
        {
            Queue.Writer.TryComplete(); // stop writers
            LifetimeCts.Cancel(); // cancel in-flight work (if any)
            try
            {
                if (Consumer is not null)
                    await Consumer.ConfigureAwait(false);
            }
            catch
            { /* swallow */
            }
            finally
            {
                LifetimeCts.Dispose();
            }
        }
    }

    public OrderedPerKeyDispatcher(
        Func<TItem, TKey> keySelector,
        Func<TItem, CancellationToken, Task> handler,
        int capacityPerKey = 1024,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait,
        bool dropIfCanceledBeforeStart = false
    )
    {
        _keySelector = keySelector;
        _handler = handler;
        _capacityPerKey = capacityPerKey;
        _fullMode = fullMode;
        _dropIfCanceledBeforeStart = dropIfCanceledBeforeStart;
    }

    // ---------- Public API ----------

    public async Task EnqueueAndWaitAsync(TItem item, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));

        if (!await TryEnqueueInternalAsync(item, tcs, ct).ConfigureAwait(false))
        {
            // Key was closed or the writer was closed during the race
            tcs.TrySetCanceled(ct);
        }

        await tcs.Task.ConfigureAwait(false);
    }

    public async Task EnqueueFireAndForgetAsync(TItem item, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!await TryEnqueueInternalAsync(item, tcs, ct).ConfigureAwait(false))
        {
            // swallow for fire-and-forget, but still complete the waiter so nothing hangs
            tcs.TrySetCanceled(ct);
        }
    }

    /// <summary>Gracefully completes a key: stops new writes, drains, then removes.</summary>
    public bool CompleteKey(TKey key)
    {
        _closedKeys[key] = 1; // mark closed so no resurrection
        if (!_queues.TryGetValue(key, out var per))
            return false;
        per.Queue.Writer.TryComplete();
        return true;
    }

    /// <summary>Hard abort: stop new writes and cancel in-flight handler (if any).</summary>
    public bool AbortKey(TKey key)
    {
        _closedKeys[key] = 1; // mark closed so no resurrection
        if (!_queues.TryGetValue(key, out var per))
            return false;
        per.Queue.Writer.TryComplete();
        per.LifetimeCts.Cancel();
        return true;
    }

    // ---------- Internals ----------

    private async Task<bool> TryEnqueueInternalAsync(
        TItem item,
        TaskCompletionSource tcs,
        CancellationToken ct
    )
    {
        var key = _keySelector(item);

        // If explicitly closed, don't create or enqueue
        if (_closedKeys.ContainsKey(key))
            return false;

        // Create/get the queue
        var per = _queues.GetOrAdd(
            key,
            static (_, s) =>
            {
                var (self, k) = s;
                var q = new PerKeyQueue(self._capacityPerKey, self._fullMode);
                q.Consumer = self.RunConsumerAsync(k, q);
                return q;
            },
            (self: this, key)
        );

        // Race: key may get closed after we fetched 'per'
        if (_closedKeys.ContainsKey(key))
        {
            // Prevent resurrection: if closed, complete writer and fail enqueue
            per.Queue.Writer.TryComplete();
            return false;
        }

        // Try fast path first to avoid awaiting if channel has space
        var work = new Work
        {
            Item = item,
            Completion = tcs,
            Ct = ct,
        };

        try
        {
            if (!per.Queue.Writer.TryWrite(work))
            {
                // May block (FullMode=Wait) or throw if writer gets closed while waiting
                await per.Queue.Writer.WriteAsync(work, ct).ConfigureAwait(false);
            }
            return true;
        }
        catch (ChannelClosedException)
        {
            // Writer just got closed (session ending). Treat as not enqueued.
            return false;
        }
        catch (OperationCanceledException)
        {
            // Publisher-side cancellation while waiting for capacity
            return false;
        }
    }

    private async Task RunConsumerAsync(TKey key, PerKeyQueue per)
    {
        try
        {
            await foreach (var work in per.Queue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                // Optional: drop if canceled before start
                if (_dropIfCanceledBeforeStart && work.Ct.IsCancellationRequested)
                {
                    work.Completion.TrySetCanceled(work.Ct);
                    continue;
                }

                // Link per-key abort with per-item token
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    work.Ct,
                    per.LifetimeCts.Token
                );

                try
                {
                    await _handler(work.Item, linked.Token).ConfigureAwait(false);
                    work.Completion.TrySetResult(); // success
                }
                catch (OperationCanceledException oce)
                {
                    work.Completion.TrySetException(oce); // canceled during execution
                }
                catch (Exception ex)
                {
                    work.Completion.TrySetException(ex); // fault
                }
            }
        }
        finally
        {
            _queues.TryRemove(key, out _);
            await per.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Mark all current keys as closed to avoid resurrection during disposal
        foreach (var key in _queues.Keys)
            _closedKeys[key] = 1;

        foreach (var (_, per) in _queues)
            await per.DisposeAsync().ConfigureAwait(false);

        _queues.Clear();
        _closedKeys.Clear();
    }
}
