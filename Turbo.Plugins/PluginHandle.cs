using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins;

public sealed class PluginHandle : IAsyncDisposable
{
    public required string Id { get; init; }
    public required Version Version { get; init; }
    public required string ShadowDir { get; init; }
    public required PluginLoadContext Alc { get; init; }
    public required Assembly PluginAssembly { get; init; }
    public required IServiceProvider PluginServices { get; init; }
    public required ITurboPlugin Plugin { get; init; }
    public List<IAsyncDisposable> Subscriptions { get; } = new();

    public async ValueTask DisposeAsync()
    {
        foreach (var s in Subscriptions)
            await s.DisposeAsync();

        if (Plugin is IAsyncDisposable ad)
            await ad.DisposeAsync();
        (PluginServices as IDisposable)?.Dispose();

        // Drop *all* host references to plugin types before calling Unload()
        // (usually handled by your PluginManager)
        Alc.Unload();
    }
}
