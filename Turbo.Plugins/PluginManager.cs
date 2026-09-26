using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Logging.Extensions;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Exceptions;
using Turbo.Plugins.Exports;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Plugins;

public sealed class PluginManager(
    IServiceProvider host,
    AssemblyProcessor processor,
    IOptions<PluginConfig> config,
    ILogger<PluginManager> logger
)
{
    private readonly ExportRegistry _exports = new();
    private readonly PluginConfig _config = config.Value;
    private readonly ILogger _logger = logger;

    private readonly ConcurrentDictionary<string, PluginEnvelope> _live = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _dependents = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly SemaphoreSlim _reloadGate = new(1, 1);

    private static readonly ServiceProviderOptions SP_OPTIONS = new()
    {
        ValidateScopes = true,
        ValidateOnBuild = false,
    };

    private List<(PluginManifest manifest, string folder)> DiscoverPlugins()
    {
        var list = new List<(PluginManifest, string)>(capacity: 16);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var devPath in _config.DevPluginPaths)
        {
            var dir = Path.GetFullPath(devPath);

            if (!Directory.Exists(dir))
                continue;

            try
            {
                var manifest = PluginHelpers.ReadManifest(dir);

                if (seen.Add(manifest.Key))
                {
                    list.Add((manifest, dir));
                    _logger.LogDebug("Discovered dev plugin {Key} from {Dir}", manifest.Key, dir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read dev plugin manifest in {Dir}", dir);
            }
        }

        if (!Directory.Exists(_config.PluginFolderPath))
            return list;
        {
            foreach (var dir in Directory.EnumerateDirectories(_config.PluginFolderPath))
            {
                try
                {
                    var manifest = PluginHelpers.ReadManifest(dir);

                    if (seen.Add(manifest.Key))
                        list.Add((manifest, dir));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read plugin manifest in {Dir}", dir);
                }
            }
        }

        return list;
    }

    /// <summary>
    /// Loads every discovered plugin in dependency order, replacing live ones. A plugin that fails
    /// is logged and skipped so the rest still load; with <paramref name="unloadRemoved"/>, live
    /// plugins whose folder is gone are unloaded.
    /// </summary>
    public async Task LoadAllAsync(bool unloadRemoved = true, CancellationToken ct = default)
    {
        await _reloadGate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var plugins = DiscoverSorted();
            var tasks = new List<Func<Task>>();

            RebuildDependents(plugins.Select(p => p.Manifest));

            foreach (var (m, folder) in plugins)
            {
                var gate = GetKeyGate(m.Key);

                await gate.WaitAsync(ct).ConfigureAwait(false);

                try
                {
                    var (asm, next) = await LoadPluginAsync(m, folder, ct).ConfigureAwait(false);

                    // Feature processing for different plugins is independent, so it runs
                    // concurrently once every plugin is live.
                    tasks.Add(async () =>
                    {
                        var disp = await processor
                            .ProcessAsync(asm.Assembly, next.ServiceProvider, ct)
                            .ConfigureAwait(false);

                        next.Disposables.Add(disp);
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to load {Name}@{Version} by {Author}",
                        m.Name,
                        m.Version,
                        m.Author
                    );
                }
                finally
                {
                    gate.Release();
                }
            }

            var degree = Math.Max(2, Environment.ProcessorCount * 4);

            await BoundedHelper.RunAsync(tasks, degree, ct).ConfigureAwait(false);

            // Only a plugin whose folder is gone counts as removed. One that failed to reload is
            // still on disk and keeps its previous live version.
            if (unloadRemoved)
                await UnloadRemovedAsync(plugins.Select(p => p.Manifest.Key), ct)
                    .ConfigureAwait(false);

            _logger.LogInformation("Loaded {Count} plugins", _live.Count);
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    /// <summary>
    /// Reloads one plugin, or unloads it when its folder is gone. Unlike
    /// <see cref="LoadAllAsync"/>, a failure is thrown to the caller, who asked for this plugin.
    /// </summary>
    public async Task ReloadAsync(string key, CancellationToken ct = default)
    {
        await _reloadGate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var plugins = DiscoverSorted();

            RebuildDependents(plugins.Select(p => p.Manifest));

            var match = plugins.FirstOrDefault(p =>
                string.Equals(p.Manifest.Key, key, StringComparison.OrdinalIgnoreCase)
            );

            if (match.Manifest is null)
            {
                await UnloadAsync(key, ct).ConfigureAwait(false);
                _logger.LogInformation("Plugin {Key} was removed from disk and unloaded.", key);
                return;
            }

            var gate = GetKeyGate(key);
            await gate.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                var (asm, next) = await LoadPluginAsync(match.Manifest, match.Folder, ct)
                    .ConfigureAwait(false);

                var disp = await processor
                    .ProcessAsync(asm.Assembly, next.ServiceProvider, ct)
                    .ConfigureAwait(false);
                next.Disposables.Add(disp);

                _logger.LogInformation("Reloaded plugin {Key}", key);
            }
            finally
            {
                gate.Release();
            }
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    /// <summary>
    /// The plugins on disk with their folders, dependencies before their dependents.
    /// </summary>
    private List<(PluginManifest Manifest, string Folder)> DiscoverSorted()
    {
        var discovered = DiscoverPlugins();
        var byKey = discovered.ToDictionary(
            d => d.manifest.Key,
            d => d.folder,
            StringComparer.OrdinalIgnoreCase
        );

        return
        [
            .. PluginHelpers
                .SortManifests([.. discovered.Select(d => d.manifest)])
                .Select(m => (m, byKey[m.Key])),
        ];
    }

    /// <summary>
    /// Replaces the live version of one plugin with a fresh load of <paramref name="folder"/>.
    /// The caller holds the plugin's key gate and runs feature processing on the result. Refuses
    /// while a dependency is not live, and refuses to replace a live version that live dependents
    /// are bound to.
    /// </summary>
    private async Task<(LoadedAssembly Assembly, PluginEnvelope Envelope)> LoadPluginAsync(
        PluginManifest m,
        string folder,
        CancellationToken ct
    )
    {
        var inactive = m.Dependencies.Select(d => d.Key).Where(k => !_live.ContainsKey(k)).ToList();

        if (inactive.Count > 0)
            throw new PluginDependencyException(
                PluginDependencyErrorType.DependencyInactive,
                m.Key,
                inactive
            );

        var current = _live.GetValueOrDefault(m.Key);

        if (
            current is not null
            && _dependents.TryGetValue(m.Key, out var deps)
            && deps.Any(_live.ContainsKey)
        )
            throw new PluginDependencyException(
                PluginDependencyErrorType.DependentsActive,
                m.Key,
                deps.Where(_live.ContainsKey)
            );

        // Checks come first so a refused load does not leave an assembly context behind.
        var asm = GetLoadedPluginAssembly(m, folder);

        if (current is not null)
            await StopAndTearDownAsync(current, ct).ConfigureAwait(false);

        var next = await BuildEnvelopeAsync(asm, m, folder, ct).ConfigureAwait(false);

        _live[m.Key] = next;

        return (asm, next);
    }

    private async Task UnloadAsync(string key, CancellationToken ct = default)
    {
        var gate = GetKeyGate(key);

        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (_dependents.TryGetValue(key, out var deps) && deps.Any(_live.ContainsKey))
                throw new PluginDependencyException(
                    PluginDependencyErrorType.DependentsActive,
                    key,
                    deps.Where(_live.ContainsKey)
                );

            if (_live.TryRemove(key, out var env))
            {
                await StopAndTearDownAsync(env, ct).ConfigureAwait(false);

                _logger.LogInformation("Unloaded {Key}", key);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task UnloadAllAsync(CancellationToken ct = default)
    {
        var keys = _live.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in _dependents)
        {
            var dep = kvp.Key;
            var list = kvp.Value.Where(keys.Contains).ToList();

            if (list.Count > 0)
                dependents[dep] = list;
        }

        while (keys.Count > 0)
        {
            var leafs = keys.Where(k => !dependents.Values.Any(list => list.Contains(k))).ToList();

            if (leafs.Count == 0)
                leafs = [.. keys];

            foreach (var k in leafs)
            {
                try
                {
                    await UnloadAsync(k, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to unload plugin {Key}", k);
                }

                keys.Remove(k);
            }
        }
    }

    private static LoadedAssembly GetLoadedPluginAssembly(PluginManifest manifest, string folder)
    {
        var asmPath = PluginHelpers.GetAssemblyPath(folder, manifest);

        return AssemblyMemoryLoader.LoadFromBytes(asmPath);
    }

    private async Task<PluginEnvelope> BuildEnvelopeAsync(
        LoadedAssembly asm,
        PluginManifest m,
        string folder,
        CancellationToken ct
    )
    {
        var inst = CreatePluginInstance(asm.Assembly);

        if (!string.Equals(inst.Key, m.Key, StringComparison.Ordinal))
            throw new PluginAssemblyException(
                PluginAssemblyErrorType.KeyMismatch,
                m.Key,
                entryPointKey: inst.Key
            );

        var sp = CreatePluginServiceProvider(inst, m);

        await inst.BindExportsAsync(new ExportBinder(_exports), sp).ConfigureAwait(false);
        await StartPluginAsync(inst, sp, ct).ConfigureAwait(false);

        return new PluginEnvelope
        {
            Key = m.Key,
            Assembly = asm.Assembly,
            Alc = asm.Alc,
            Manifest = m,
            Folder = folder,
            Instance = inst,
            ServiceProvider = sp,
            Disposables = [sp],
        };
    }

    private async Task UnloadRemovedAsync(IEnumerable<string> keys, CancellationToken ct)
    {
        var keep = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        var removed = _live.Keys.Where(k => !keep.Contains(k)).ToList();

        foreach (var k in removed)
        {
            try
            {
                await UnloadAsync(k, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unload removed plugin {Key}", k);
            }
        }
    }

    private static ITurboPlugin CreatePluginInstance(Assembly asm)
    {
        var pluginType =
            AssemblyExplorer.FindType(asm, typeof(ITurboPlugin))
            ?? throw new PluginAssemblyException(
                PluginAssemblyErrorType.EntryPointNotFound,
                assemblyLocation: asm.GetName().Name
            );

        return (ITurboPlugin)Activator.CreateInstance(pluginType)!;
    }

    private ServiceProvider CreatePluginServiceProvider(
        ITurboPlugin plugin,
        PluginManifest manifest
    )
    {
        var services = new ServiceCollection();

        services.AddSingleton(manifest);
        services.AddSingleton<IPluginCatalog>(new PluginCatalog(_exports));
        services.AddSingleton<IHostServices>(new HostServices(host));
        services.ConfigurePrefixedLogging(host, manifest.Name);

        plugin.ConfigureServices(services, manifest);

        return services.BuildServiceProvider(SP_OPTIONS);
    }

    private async Task StartPluginAsync(
        ITurboPlugin plugin,
        IServiceProvider sp,
        CancellationToken ct
    )
    {
        await ProcessMigrationsAsync(sp, ct).ConfigureAwait(false);

        foreach (var svc in sp.GetServices<IHostedService>())
        {
            try
            {
                await svc.StartAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // The plugin still starts; it decides in its own Start whether it can run without
                // the service.
                _logger.LogError(
                    ex,
                    "Hosted service {Service} of a plugin failed to start",
                    svc.GetType().FullName
                );
            }
        }

        await plugin.StartAsync(sp, ct).ConfigureAwait(false);
    }

    private static async Task ProcessMigrationsAsync(
        IServiceProvider pluginRoot,
        CancellationToken ct
    )
    {
        var scope = pluginRoot.CreateAsyncScope();

        try
        {
            var sp = scope.ServiceProvider;
            var dbModule = sp.GetService<IPluginDbModule>();

            if (dbModule is null)
                return;

            await dbModule.MigrateAsync(sp, ct).ConfigureAwait(false);
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task StopAndTearDownAsync(PluginEnvelope env, CancellationToken ct)
    {
        try
        {
            if (env.ServiceProvider is { } sp)
            {
                foreach (var svc in sp.GetServices<IHostedService>())
                {
                    try
                    {
                        await svc.StopAsync(ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Hosted service stop failed for {Key}",
                            env.Manifest.Key
                        );
                    }
                }
            }

            try
            {
                await env.Instance.StopAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop plugin {Key}", env.Manifest.Key);
            }

            if (env.Disposables.Count > 0)
            {
                foreach (var inst in env.Disposables)
                {
                    try
                    {
                        switch (inst)
                        {
                            case IAsyncDisposable iad:
                                await iad.DisposeAsync().ConfigureAwait(false);
                                continue;
                            case IDisposable d:
                                d.Dispose();
                                continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to dispose {Type} for {Key}",
                            inst.GetType().Name,
                            env.Manifest.Key
                        );
                    }
                }

                env.Disposables.Clear();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to destroy {Key}", env.Manifest.Key);
        }

        if (env.Alc is not null)
        {
            var unloaded = await AssemblyMemoryLoader
                .UnloadAndWaitAsync(env.Alc, 5000, ct)
                .ConfigureAwait(false);

            if (!unloaded)
                _logger.LogWarning(
                    "ALC for plugin {Key} did not unload within timeout. Possible memory leak from retained type references.",
                    env.Manifest.Key
                );
        }
    }

    private SemaphoreSlim GetKeyGate(string key) =>
        _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

    private void RebuildDependents(IEnumerable<PluginManifest> manifests)
    {
        _dependents.Clear();

        foreach (var manifest in manifests)
        {
            foreach (var dep in manifest.Dependencies)
                _dependents.GetOrAdd(dep.Key, _ => []).Add(manifest.Key);
        }
    }
}
