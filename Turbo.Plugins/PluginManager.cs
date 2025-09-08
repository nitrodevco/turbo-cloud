using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Plugins;
using Turbo.Database.Configuration;
using Turbo.Database.Delegates;
using Turbo.Plugins.Configuration;

namespace Turbo.Plugins;

public class PluginManager(IServiceProvider host, ILogger<PluginManager> log, PluginConfig config)
    : IAsyncDisposable
{
    private readonly IServiceProvider _host = host;
    private readonly ILogger<PluginManager> _log = log;
    private readonly PluginConfig _config = config;

    private readonly ConcurrentDictionary<string, PluginHandle> _loaded = new();

    public async Task LoadOrReloadAllPlugins(CancellationToken ct = default)
    {
        foreach (var dir in Directory.EnumerateDirectories(_config.PluginFolderPath))
            await LoadOrReloadAsync(dir, ct);
    }

    public async Task LoadOrReloadAsync(string pluginSourceDir, CancellationToken ct = default)
    {
        // 1) Read manifest → gives you pluginId
        var manifest = PluginHelpers.ReadManifest(pluginSourceDir);
        var pluginId = manifest.Id;

        // 2) Shadow copy using pluginId
        var shadowDir = PluginHelpers.CreateShadowCopy(pluginSourceDir, pluginId);

        // 3) Create collectible ALC + load entry assembly
        var alc = new PluginLoadContext(shadowDir);
        var entryPath = PluginHelpers.FindEntryAssemblyPath(shadowDir, manifest);
        Assembly asm = null!;
        try
        {
            asm = alc.LoadFromAssemblyPath(entryPath);
        }
        catch
        {
            alc.Unload();
            PluginHelpers.TryDeleteDirectory(shadowDir);
            throw;
        }

        // 4) If previously loaded, unload old one first
        if (_loaded.TryRemove(pluginId, out var old))
            await UnloadInternalAsync(old, TimeSpan.FromSeconds(15));

        // 5) Build DI, migrate DB, wire handlers, enable
        var handle = await BuildAndEnableAsync(alc, asm, manifest, shadowDir, ct);

        // 6) Track
        _loaded[pluginId] = handle;
        _log.LogInformation(
            "Plugin {PluginId} v{Version} loaded from {Dir}",
            pluginId,
            manifest.Version,
            pluginSourceDir
        );
    }

    public async Task UnloadByIdAsync(string pluginId, TimeSpan timeout)
    {
        if (_loaded.TryRemove(pluginId, out var h))
            await UnloadInternalAsync(h, timeout);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var h in _loaded.Values)
            await UnloadInternalAsync(h, TimeSpan.FromSeconds(10));
        _loaded.Clear();
    }

    // ----- internals -----

    private async Task<PluginHandle> BuildAndEnableAsync(
        PluginLoadContext alc,
        Assembly asm,
        PluginManifest manifest,
        string shadowDir,
        CancellationToken ct
    )
    {
        var pluginType =
            asm.GetTypes().First(t => typeof(ITurboPlugin).IsAssignableFrom(t) && !t.IsAbstract)
            ?? throw new InvalidOperationException($"No ITurboPlugin found in {shadowDir}.");
        var plugin = (ITurboPlugin)Activator.CreateInstance(pluginType)!;

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(_host.GetRequiredService<DatabaseConfig>());
        services.AddSingleton(_host.GetRequiredService<ILoggerFactory>());
        services.AddSingleton(manifest);
        services.AddSingleton<TablePrefixProvider>(sp =>
        {
            var manifest = sp.GetRequiredService<PluginManifest>();

            return () => manifest.TablePrefix ?? string.Empty;
        });

        if (plugin.RequiredHostServices?.Count > 0)
        {
            foreach (var t in plugin.RequiredHostServices)
                services.AddSingleton(_ => _host.GetRequiredService(t));
        }

        plugin.ConfigureServices(services);

        var sp = services.BuildServiceProvider();

        var dbModule = sp.GetService<IPluginDbModule>();

        if (dbModule is not null)
            await dbModule.MigrateAsync(sp, ct);

        await plugin.OnEnableAsync(ct);

        return new PluginHandle
        {
            Id = manifest.Id,
            Version = Version.Parse(manifest.Version),
            ShadowDir = shadowDir,
            Alc = alc,
            PluginAssembly = asm,
            PluginServices = sp,
            Plugin = plugin,
        };
    }

    private async Task UnloadInternalAsync(PluginHandle h, TimeSpan timeout)
    {
        try
        {
            await h.Plugin.OnDisableAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Plugin {Id} OnDisable failed", h.Id);
        }

        (h.PluginServices as IDisposable)?.Dispose();
        await h.Plugin.DisposeAsync();

        h.Alc.Unload();

        var weak = new WeakReference(h.Alc);
        for (var i = 0; i < 2 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        if (weak.IsAlive)
            _log.LogWarning("Plugin {Id} did not unload (leaked refs).", h.Id);

        PluginHelpers.TryDeleteDirectory(h.ShadowDir);
        _log.LogInformation("Plugin {Id} unloaded.", h.Id);
    }
}
