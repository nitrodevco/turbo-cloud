using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Exports;

namespace PurseTheme;

/// <summary>
/// A theme with no code of its own: everything it changes is in <c>wwwroot/template-patches.json</c>,
/// which the server lists for clients while the plugin is loaded. The class exists because a plugin
/// is loaded by its assembly.
/// </summary>
public sealed class PurseThemePlugin : ITurboPlugin
{
    public string Key => "pursetheme";

    public string Version => "1.0.0";

    public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }

    public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
