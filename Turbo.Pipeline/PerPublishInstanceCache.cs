using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline;

internal class PerPublishInstanceCache : IAsyncDisposable
{
    private readonly ConcurrentDictionary<(string ownerId, Type impl), Lazy<object>> _cache = new(
        Environment.ProcessorCount,
        32
    );
    private readonly ConcurrentBag<object> _toDispose = new();

    public object GetOrAdd((string ownerId, Type impl) key, Func<object> factory)
    {
        var lazy = _cache.GetOrAdd(
            key,
            _ => new Lazy<object>(
                () =>
                {
                    var obj = factory();
                    // Only the top-level object created by ActivatorUtilities needs manual disposal.
                    if (obj is IAsyncDisposable || obj is IDisposable)
                        _toDispose.Add(obj);
                    return obj;
                },
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );
        return lazy.Value;
    }

    public async ValueTask DisposeAsync()
    {
        // Dispose in LIFO-ish order isn’t guaranteed here; good enough for handlers.
        foreach (var obj in _toDispose)
        {
            switch (obj)
            {
                case IAsyncDisposable iad:
                    await iad.DisposeAsync().ConfigureAwait(false);
                    break;
                case IDisposable id:
                    id.Dispose();
                    break;
            }
        }
    }
}
