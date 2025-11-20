using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Turbo.Runtime;

public sealed class CompositeServiceProviderBag(IServiceProvider baseSp)
{
    private readonly IServiceProvider _baseSp = baseSp;
    private readonly ConcurrentDictionary<IServiceProvider, Lazy<IServiceProvider>> _byOwner = new(
        concurrencyLevel: Environment.ProcessorCount,
        capacity: 4
    );

    public IServiceProvider Get(IServiceProvider owner)
    {
        var lazy = _byOwner.GetOrAdd(
            owner,
            o => new Lazy<IServiceProvider>(
                () => new CompositeServiceProvider(_baseSp, o),
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );

        return lazy.Value;
    }
}
