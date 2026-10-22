using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Exports;
using Turbo.Primitives.ClientModules;

namespace Hello;

/// <summary>
/// The smallest plugin with a client module: it declares <c>wwwroot/hello.js</c>, which a client
/// that speaks <c>client.modules</c> loads and runs, and its icon <c>wwwroot/hello.png</c>. Its texts and its bar's
/// colour and width come with it in <c>gamedata/</c>, given to the hotel as it loads. See <c>docs/client-modules.md</c>.
/// </summary>
public sealed class HelloPlugin : ITurboPlugin
{
    private IDisposable? _module;

    public string Key => "hello";

    public string Version => "1.0.0";

    public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }

    public Task StartAsync(IServiceProvider services, CancellationToken ct)
    {
        _module = services
            .GetRequiredService<IHostServices>()
            .GetRequiredService<IClientModuleRegistry>()
            .Register(Key, "hello.js");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        // The server takes it away on unload anyway; a plugin that can switch its module off at
        // run time disposes the handle itself.
        _module?.Dispose();
        _module = null;

        return Task.CompletedTask;
    }

    public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
