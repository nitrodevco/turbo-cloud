using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins;

internal class NoopTurboPlugin : ITurboPlugin
{
    public List<Type> RequiredHostServices => [];

    public void ConfigureServices(IServiceCollection services) { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public ValueTask OnEnableAsync(IServiceProvider sp, CancellationToken ct) =>
        ValueTask.CompletedTask;

    public ValueTask OnDisableAsync(IServiceProvider sp, CancellationToken ct) =>
        ValueTask.CompletedTask;
}
