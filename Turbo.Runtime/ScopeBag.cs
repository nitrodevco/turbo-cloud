using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Runtime;

public sealed class ScopeBag : IAsyncDisposable
{
    private readonly Dictionary<IServiceProvider, IServiceScope> _byOwner = new(
        ReferenceEqualityComparer.Instance
    );

    public IServiceProvider Get(IServiceProvider owner)
    {
        if (!_byOwner.TryGetValue(owner, out var scope))
        {
            var factory = owner.GetRequiredService<IServiceScopeFactory>();
            scope = factory.CreateScope();
            _byOwner.Add(owner, scope);
        }
        return scope.ServiceProvider;
    }

    public ValueTask DisposeAsync()
    {
        foreach (var s in _byOwner.Values)
            s.Dispose();
        _byOwner.Clear();
        return ValueTask.CompletedTask;
    }
}
