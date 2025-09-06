using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Core;

public sealed class OrderedPerKeyDispatcher<TKey> : IAsyncDisposable
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, PerKeyQueue> _queues = new();
    private readonly TimeSpan _idleTimeout;
    private readonly BoundedChannelOptions _channelOptions;

    public OrderedPerKeyDispatcher(
        int perKeyCapacity = 512,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait,
        TimeSpan? idleTimeout = null
    )
    {
        _idleTimeout = idleTimeout ?? TimeSpan.FromMinutes(2);
        _channelOptions = new BoundedChannelOptions(perKeyCapacity)
        {
            FullMode = fullMode,
            SingleReader = true,
            SingleWriter = false,
        };
    }

    public ValueTask EnqueueAsync(
        TKey key,
        Func<CancellationToken, ValueTask> work,
        CancellationToken ct = default
    )
    {
        var q = _queues.GetOrAdd(
            key,
            k => new PerKeyQueue(
                _channelOptions,
                _idleTimeout,
                () =>
                {
                    // cleanup callback when queue completes and is idle
                    _queues.TryRemove(k, out _);
                }
            )
        );

        return q.EnqueueAsync(work, ct);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var q in _queues.Values)
            await q.DisposeAsync().ConfigureAwait(false);
        _queues.Clear();
    }

    private sealed class PerKeyQueue : IAsyncDisposable
    {
        private readonly Channel<Func<CancellationToken, ValueTask>> _ch;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _pump;
        private readonly TimeSpan _idleTimeout;
        private readonly Action _onCompleted;

        public PerKeyQueue(BoundedChannelOptions opts, TimeSpan idleTimeout, Action onCompleted)
        {
            _idleTimeout = idleTimeout;
            _onCompleted = onCompleted;
            _ch = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(opts);
            _pump = Task.Run(PumpAsync);
        }

        public async ValueTask EnqueueAsync(
            Func<CancellationToken, ValueTask> work,
            CancellationToken ct
        )
        {
            // propagate cancellation to the write only
            using var l = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            await _ch.Writer.WriteAsync(work, l.Token).ConfigureAwait(false);
        }

        private async Task PumpAsync()
        {
            try
            {
                var reader = _ch.Reader;

                while (!_cts.IsCancellationRequested)
                {
                    // Try immediate read
                    if (reader.TryRead(out var work))
                    {
                        await SafeRun(work).ConfigureAwait(false);
                        continue;
                    }

                    // Wait for data with idle timeout
                    using var idleCts = new CancellationTokenSource(_idleTimeout);
                    try
                    {
                        work = await reader.ReadAsync(idleCts.Token).ConfigureAwait(false);
                        await SafeRun(work).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (idleCts.IsCancellationRequested)
                    {
                        // Idle -> complete and exit
                        _ch.Writer.TryComplete();
                        break;
                    }
                }
            }
            catch (ChannelClosedException)
            { /* normal */
            }
            catch (Exception)
            { /* swallow pump error, queue will be GC'd */
            }
            finally
            {
                _onCompleted();
            }
        }

        private static async Task SafeRun(Func<CancellationToken, ValueTask> work)
        {
            try
            {
                await work(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // swallow per-item exceptions here so the pump keeps running.
                // upstream code should handle/report its own errors.
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _ch.Writer.TryComplete();
            try
            {
                await _pump.ConfigureAwait(false);
            }
            catch
            { /* ignore */
            }
            _cts.Dispose();
        }
    }
}
