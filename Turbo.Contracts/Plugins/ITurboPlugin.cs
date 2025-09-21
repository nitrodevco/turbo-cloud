using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins.Exports;

namespace Turbo.Contracts.Plugins;

public interface ITurboPlugin : IAsyncDisposable
{
    string Key { get; }
    string Version { get; }

    void ConfigureServices(IServiceCollection services, PluginManifest manifest);

    // Start and Stop are lifecycle hooks inside the plugin scope
    Task StartAsync(IServiceProvider services, CancellationToken ct);
    Task StopAsync(CancellationToken ct);

    Task BindExportsAsync(IExportBinder binder, IServiceProvider services);
}
