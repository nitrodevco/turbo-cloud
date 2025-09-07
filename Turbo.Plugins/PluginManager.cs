using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scrutor;
using Turbo.Contracts.Plugins;
using Turbo.Database.Configuration;
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
        var manifest = PluginManifestUtil.ReadManifest(pluginSourceDir);
        var pluginId = manifest.Id;

        // 2) Shadow copy using pluginId
        var shadowDir = PluginShadowHelper.CreateShadowCopy(pluginSourceDir, pluginId);

        // 3) Create collectible ALC + load entry assembly
        var alc = new PluginLoadContext(shadowDir);
        var entryPath = PluginPathHelper.FindEntryAssemblyPath(shadowDir, manifest);
        Assembly asm = null!;
        try
        {
            asm = alc.LoadFromAssemblyPath(entryPath);
        }
        catch
        {
            alc.Unload();
            PluginShadowHelper.TryDeleteDirectory(shadowDir);
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
        // Build child DI (uses manifest.Id as pluginId)
        var sp = BuildPluginServiceProvider(asm, manifest);

        // Run plugin DB migrations BEFORE enabling handlers
        foreach (var m in sp.GetServices<IPluginDbModule>())
            await m.MigrateAsync(sp, ct);

        // Create plugin + OnLoad
        var pluginType = asm.GetTypes()
            .First(t => typeof(ITurboPlugin).IsAssignableFrom(t) && !t.IsAbstract);
        var plugin = (ITurboPlugin)ActivatorUtilities.CreateInstance(sp, pluginType);

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

    private IServiceProvider BuildPluginServiceProvider(Assembly pluginAsm, PluginManifest manifest)
    {
        var services = new ServiceCollection();

        services.AddSingleton(_ =>
            PluginRuntimeConfig.BuildFromManifest(
                manifest,
                _host.GetRequiredService<DatabaseConfig>().ConnectionString
            )
        );

        // expose limited host services
        services.AddSingleton(_ => _host.GetRequiredService<ILoggerFactory>());
        services.AddLogging();
        services.AddSingleton(_ => _host.GetRequiredService<DatabaseConfig>());

        // let the plugin register its own services (DbContext, repos, handlers)
        foreach (var t in pluginAsm.GetTypes())
        {
            if (
                typeof(IRegistersServices).IsAssignableFrom(t)
                && !t.IsAbstract
                && t.GetConstructor(Type.EmptyTypes) != null
            )
            {
                var reg = (IRegistersServices)ActivatorUtilities.CreateInstance(_host, t);
                reg.ConfigureServices(services);
            }
        }

        // add plugin db modules (migrations)
        services.Scan(s =>
            s.FromAssemblies(pluginAsm)
                .AddClasses(c => c.AssignableTo<IPluginDbModule>())
                .AsImplementedInterfaces()
                .WithTransientLifetime()
        );

        // make the manifest available to plugin code if needed
        services.AddSingleton(manifest);

        return services.BuildServiceProvider();
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

        PluginShadowHelper.TryDeleteDirectory(h.ShadowDir);
        _log.LogInformation("Plugin {Id} unloaded.", h.Id);
    }
}
