using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Exports;
using Turbo.Primitives.ClientModules;

namespace InfostandSkin;

/// <summary>
/// A skin that changes windows three ways, with no server logic of its own:
/// <list type="bullet">
/// <item>its client module (<c>wwwroot/skin.js</c>) skins the user infostand by the user it shows -
/// the window's context, an override binding, a move, layout params and a post-layout hook;</item>
/// <item>its <c>gamedata/external_variables.json</c> recolours the wired setup window, which is drawn
/// without a template (<c>frame.overrides</c>), and replaces the default window frame's art with
/// <c>wwwroot/frame.png</c> (<c>asset.overlays</c>, a path in its wwwroot the server gives clients
/// the address of).</item>
/// </list>
/// The class registers the module; the variables need nothing but the file being there.
/// </summary>
public sealed class InfostandSkinPlugin : ITurboPlugin
{
    private IDisposable? _module;

    public string Key => "infostandskin";

    public string Version => "1.0.0";

    public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }

    public Task StartAsync(IServiceProvider services, CancellationToken ct)
    {
        _module = services
            .GetRequiredService<IHostServices>()
            .GetRequiredService<IClientModuleRegistry>()
            .Register(Key, "skin.js");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _module?.Dispose();
        _module = null;

        return Task.CompletedTask;
    }

    public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
