using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Runtime;

public sealed class JoinedScope(
    IServiceScope primaryScope,
    IServiceScope secondaryScope,
    bool disposeSecondary = true
) : IServiceScope, IServiceProvider, IAsyncDisposable
{
    private readonly IServiceScope _primaryScope = primaryScope;
    private readonly IServiceScope _secondaryScope = secondaryScope;
    private readonly bool _disposeSecondary = disposeSecondary;
    public IServiceProvider ServiceProvider => this;

    public object? GetService(Type serviceType)
    {
        var svc = _primaryScope.ServiceProvider.GetService(serviceType);

        return svc ?? _secondaryScope.ServiceProvider.GetService(serviceType);
    }

    public object GetRequiredService(Type serviceType)
    {
        var svc = _primaryScope.ServiceProvider.GetService(serviceType);

        return svc ?? _secondaryScope.ServiceProvider.GetRequiredService(serviceType);
    }

    public void Dispose()
    {
        _primaryScope.Dispose();

        if (_disposeSecondary)
            _secondaryScope.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_primaryScope is IAsyncDisposable had)
            await had.DisposeAsync().ConfigureAwait(false);
        else
            _primaryScope.Dispose();

        if (_disposeSecondary)
        {
            if (_secondaryScope is IAsyncDisposable pad)
                await pad.DisposeAsync().ConfigureAwait(false);
            else
                _secondaryScope.Dispose();
        }
    }
}
