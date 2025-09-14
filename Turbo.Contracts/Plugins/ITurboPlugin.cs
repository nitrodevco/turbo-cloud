using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Contracts.Plugins;

public interface ITurboPlugin : IAsyncDisposable
{
    public List<Type> RequiredHostServices { get; }
    void ConfigureServices(IServiceCollection services);
    ValueTask OnEnableAsync(IServiceProvider sp, CancellationToken ct);
    ValueTask OnDisableAsync(IServiceProvider sp, CancellationToken ct);
}
