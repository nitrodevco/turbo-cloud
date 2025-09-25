using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Runtime;

public sealed class ScopeBag : IAsyncDisposable
{
    private readonly ConcurrentDictionary<IServiceProvider, Lazy<IServiceScope>> _byOwner = new();

    public IServiceProvider Get(IServiceProvider owner)
    {
        var lazy = _byOwner.GetOrAdd(
            owner,
            o => new Lazy<IServiceScope>(() =>
            {
                var factory = o.GetRequiredService<IServiceScopeFactory>();
                return factory.CreateScope();
            })
        );

        return lazy.Value.ServiceProvider;
    }

    public ValueTask DisposeAsync()
    {
        foreach (var kv in _byOwner)
        {
            try
            {
                if (kv.Value.IsValueCreated)
                    kv.Value.Value.Dispose();
            }
            catch
            {
                // swallow - disposal best-effort
            }
        }

        _byOwner.Clear();
        return ValueTask.CompletedTask;
    }
}
