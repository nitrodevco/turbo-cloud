using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Runtime;

public sealed class ScopeBag : IAsyncDisposable
{
    private readonly ConcurrentDictionary<IServiceProvider, IServiceScope> _byOwner = new();

    public IServiceProvider Get(IServiceProvider owner)
    {
        var scope = _byOwner.GetOrAdd(
            owner,
            o =>
            {
                var factory = o.GetRequiredService<IServiceScopeFactory>();
                return factory.CreateScope();
            }
        );

        return scope.ServiceProvider;
    }

    public ValueTask DisposeAsync()
    {
        foreach (var s in _byOwner.Values)
        {
            try
            {
                s.Dispose();
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
