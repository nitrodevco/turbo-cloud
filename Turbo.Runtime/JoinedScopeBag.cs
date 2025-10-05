using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Runtime;

public sealed class JoinedScopeBag(IServiceScope baseScope) : IAsyncDisposable
{
    private readonly IServiceScope _baseScope = baseScope;
    private readonly ConcurrentDictionary<IServiceProvider, Lazy<IServiceScope>> _byOwner = new(
        concurrencyLevel: Environment.ProcessorCount,
        capacity: 4
    );

    public IServiceProvider Get(IServiceProvider owner)
    {
        var lazy = _byOwner.GetOrAdd(
            owner,
            o => new Lazy<IServiceScope>(
                () => new JoinedScope(o.CreateAsyncScope(), _baseScope, false),
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );

        return lazy.Value.ServiceProvider;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var kv in _byOwner)
        {
            var lazy = kv.Value;

            if (!lazy.IsValueCreated)
                continue;

            try
            {
                if (lazy.Value is IAsyncDisposable pad)
                    await pad.DisposeAsync().ConfigureAwait(false);
                else
                    lazy.Value.Dispose();
            }
            catch
            {
                // best-effort disposal; swallow
            }
        }

        if (_baseScope is IAsyncDisposable bad)
            await bad.DisposeAsync().ConfigureAwait(false);
        else
            _baseScope.Dispose();

        _byOwner.Clear();
    }
}
