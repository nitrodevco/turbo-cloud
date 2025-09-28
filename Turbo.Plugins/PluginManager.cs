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
using Turbo.Plugins.Exports;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Plugins;

public sealed class PluginManager(
    IEnumerable<IHostPlugin> hostPlugins,
    IServiceProvider host,
    AssemblyProcessor processor,
    IOptions<PluginConfig> config,
    ILogger<PluginManager> logger
)
{
    private readonly IEnumerable<IHostPlugin> _hostPlugins = hostPlugins ?? [];
    private readonly IServiceProvider _host = host;
    private readonly AssemblyProcessor _processor = processor;
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

    private static readonly ServiceProviderOptions SP_OPTIONS = new()
    {
        ValidateScopes = true,
        ValidateOnBuild = false,
    };

    private List<(PluginManifest manifest, string folder)> Discover()
    {
        var list = new List<(PluginManifest, string)>(capacity: 16);

        if (!Directory.Exists(_config.PluginFolderPath))
            return list;

        foreach (var dir in Directory.EnumerateDirectories(_config.PluginFolderPath))
        {
            try
            {
                var manifest = PluginHelpers.ReadManifest(dir);

                if (manifest is not null)
                    list.Add((manifest, dir));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read plugin manifest in {Dir}", dir);
            }
        }

        return list;
    }

    private static AssemblyDescriptor MakeDescriptor(
        string key,
        Assembly asm,
        ByteLoadingAlc? alc = null
    ) =>
        new()
        {
            Key = key,
            Assembly = asm,
            Alc = alc,
        };

    public IEnumerable<AssemblyDescriptor> GetInternalAssemblyDescriptors()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var plugin in _hostPlugins)
        {
            if (plugin is null)
                continue;

            var key = plugin.Key;

            if (!seen.Add(key))
                continue;

            var asm = plugin.GetType().Assembly;

            yield return MakeDescriptor(key, asm);
        }
    }

    public IEnumerable<(
        AssemblyDescriptor descriptor,
        PluginManifest manifest,
        string folder
    )> GetExternalAssemblyDescriptors()
    {
        var discovered = Discover();

        if (discovered.Count == 0)
            yield break;

        var manifests = PluginHelpers.SortManifests(discovered.Select(d => d.manifest).ToArray());
        var byKey = discovered.ToDictionary(
            d => d.manifest.Key,
            d => d.folder,
            StringComparer.OrdinalIgnoreCase
        );

        _dependents.Clear();

        foreach (var m in manifests)
        {
            foreach (var dep in m.Dependencies)
                _dependents.GetOrAdd(dep.Key, _ => new()).Add(m.Key);

            var folder = byKey[m.Key];
            var loaded = GetLoadedPluginAssembly(m, folder);

            yield return (MakeDescriptor(m.Key, loaded.Assembly, loaded.Alc), m, folder);
        }
    }

    public async Task LoadAllAsync(bool unloadRemoved = true, CancellationToken ct = default)
    {
        var internalDescriptors = GetInternalAssemblyDescriptors().ToArray();
        var externalTriplets = GetExternalAssemblyDescriptors().ToArray();
        var tasks = new List<Func<Task>>(internalDescriptors.Length + externalTriplets.Length);

        foreach (var d in internalDescriptors)
            tasks.Add(() => _processor.ProcessAsync(d.Assembly, _host, ct));

        foreach (var (descriptor, manifest, folder) in externalTriplets)
        {
            try
            {
                var env = await BuildEnvelopeAsync(descriptor, manifest, folder, ct)
                    .ConfigureAwait(false);
                var old = _live.GetValueOrDefault(manifest.Key);

                _live[manifest.Key] = env;

                tasks.Add(async () =>
                {
                    var disp = await _processor
                        .ProcessAsync(descriptor.Assembly, env.ServiceProvider, ct)
                        .ConfigureAwait(false);
                    env.Disposables.Add(disp);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load {Name}@{Version} by {Author}",
                    manifest.Name,
                    manifest.Version,
                    manifest.Author
                );
            }
        }

        var degree = Math.Max(2, Environment.ProcessorCount * 4);

        await RunBoundedAsync(tasks, degree, ct).ConfigureAwait(false);

        if (unloadRemoved)
            await UnloadRemovedAsync(
                    internalDescriptors.Select(d => d.Key),
                    externalTriplets.Select(t => t.manifest.Key),
                    ct
                )
                .ConfigureAwait(false);

        _logger.LogInformation("Loaded {Count} plugins", _live.Count);
    }

    public async Task ReloadAsync(string key, CancellationToken ct = default)
    {
        var gate = GetKeyGate(key);

        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (_dependents.TryGetValue(key, out var deps) && deps.Any(_live.ContainsKey))
                throw new InvalidOperationException(
                    $"Cannot reload {key} while dependents are active: {string.Join(",", deps.Where(_live.ContainsKey))}"
                );

            if (!_live.TryGetValue(key, out var current))
                throw new InvalidOperationException($"Plugin {key} is not loaded");

            var manifest = PluginHelpers.ReadManifest(current.Folder);
            var loaded = GetLoadedPluginAssembly(manifest, current.Folder);
            var descriptor = MakeDescriptor(key, loaded.Assembly, loaded.Alc);
            var next = await BuildEnvelopeAsync(descriptor, manifest, current.Folder, ct)
                .ConfigureAwait(false);

            _live[key] = next;

            await StopAndTearDownAsync(current, ct).ConfigureAwait(false);

            _logger.LogInformation("Reloaded {Key} -> {Version}", key, manifest.Version);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task UnloadAsync(string key, CancellationToken ct = default)
    {
        var gate = GetKeyGate(key);

        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (_dependents.TryGetValue(key, out var deps) && deps.Any(_live.ContainsKey))
                throw new InvalidOperationException(
                    $"Cannot unload {key}; dependents active: {string.Join(",", deps.Where(_live.ContainsKey))}"
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
        AssemblyDescriptor descriptor,
        PluginManifest manifest,
        string folder,
        CancellationToken ct
    )
    {
        var plugin = CreatePluginInstance(descriptor.Assembly, manifest);

        if (!string.Equals(plugin.Key, manifest.Key, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Plugin key mismatch: manifest={manifest.Key} entry={plugin.Key}"
            );

        var sp = CreatePluginServiceProvider(plugin, manifest);

        await plugin.BindExportsAsync(new ExportBinder(_exports), sp).ConfigureAwait(false);
        await ProcessMigrationsAsync(sp, ct).ConfigureAwait(false);
        await StartPluginAsync(plugin, sp, ct).ConfigureAwait(false);

        return new PluginEnvelope
        {
            Key = descriptor.Key,
            Assembly = descriptor.Assembly,
            Alc = descriptor.Alc,
            Manifest = manifest,
            Folder = folder,
            Instance = plugin,
            ServiceProvider = sp,
            Disposables = [sp],
        };
    }

    private static async Task RunBoundedAsync(
        IEnumerable<Func<Task>> tasks,
        int degree,
        CancellationToken ct
    )
    {
        var sem = new SemaphoreSlim(degree);
        var running = new List<Task>();

        foreach (var t in tasks)
        {
            await sem.WaitAsync(ct).ConfigureAwait(false);

            var task = Task.Run(
                async () =>
                {
                    try
                    {
                        await t().ConfigureAwait(false);
                    }
                    finally
                    {
                        sem.Release();
                    }
                },
                ct
            );

            running.Add(task);
        }

        await Task.WhenAll(running).ConfigureAwait(false);
    }

    private async Task UnloadRemovedAsync(
        IEnumerable<string> internalKeys,
        IEnumerable<string> externalKeys,
        CancellationToken ct
    )
    {
        var keep = new HashSet<string>(
            internalKeys.Concat(externalKeys),
            StringComparer.OrdinalIgnoreCase
        );
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

    private ITurboPlugin CreatePluginInstance(Assembly asm, PluginManifest manifest)
    {
        try
        {
            var pluginType =
                AssemblyExplorer.FindType(asm, typeof(ITurboPlugin))
                ?? throw new InvalidOperationException(
                    $"Failed to find ITurboPlugin in assembly '{asm.GetName().Name}'."
                );
            return (ITurboPlugin)Activator.CreateInstance(pluginType)!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create plugin instance for {Key}", manifest.Key);
            throw;
        }
    }

    private ServiceProvider CreatePluginServiceProvider(
        ITurboPlugin plugin,
        PluginManifest manifest
    )
    {
        var services = new ServiceCollection();

        services.AddSingleton(manifest);
        services.AddSingleton<IPluginCatalog>(new PluginCatalog(_exports));
        services.AddSingleton<IHostServices>(new HostServices(_host));
        services.ConfigurePrefixedLogging(_host, manifest.Name);

        plugin.ConfigureServices(services, manifest);

        return services.BuildServiceProvider(SP_OPTIONS);
    }

    private static async Task StartPluginAsync(
        ITurboPlugin plugin,
        IServiceProvider sp,
        CancellationToken ct
    )
    {
        foreach (var svc in sp.GetServices<IHostedService>())
        {
            try
            {
                await svc.StartAsync(ct).ConfigureAwait(false);
            }
            catch
            { /* bubble via plugin Start */
            }
        }

        await plugin.StartAsync(sp, ct).ConfigureAwait(false);
    }

    private static async Task ProcessMigrationsAsync(
        IServiceProvider pluginRoot,
        CancellationToken ct
    )
    {
        await using var scope = pluginRoot.CreateAsyncScope();
        var dbModule = scope.ServiceProvider.GetService<IPluginDbModule>();
        if (dbModule is not null)
            await dbModule.MigrateAsync(scope.ServiceProvider, ct).ConfigureAwait(false);
    }

    private async Task StopAndTearDownAsync(PluginEnvelope env, CancellationToken ct)
    {
        try
        {
            try
            {
                await env.Instance.StopAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop plugin {Key}", env.Manifest.Key);
            }

            if (env.ServiceProvider is IServiceProvider sp)
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
                                break;
                            case IDisposable d:
                                d.Dispose();
                                break;
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
            await AssemblyMemoryLoader
                .UnloadAndWaitAsync(env.Alc, default, ct)
                .ConfigureAwait(false);
    }

    private SemaphoreSlim GetKeyGate(string key) =>
        _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
}
