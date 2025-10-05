using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Runtime;

internal sealed class AsyncScopeHolder : IAsyncDisposable
{
    public AsyncServiceScope Scope;
    public IServiceProvider ServiceProvider => Scope.ServiceProvider;

    public ValueTask DisposeAsync() => Scope.DisposeAsync();
}
