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

    private List<(PluginManifest manifest, string folder)> DiscoverPlugins()
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

    public async Task LoadAllAsync(bool unloadRemoved = true, CancellationToken ct = default)
    {
        var discovered = DiscoverPlugins();
        var manifests = PluginHelpers.SortManifests([.. discovered.Select(d => d.manifest)]);
        var byKey = discovered.ToDictionary(
            d => d.manifest.Key,
            d => d.folder,
            StringComparer.OrdinalIgnoreCase
        );
        var envs = new List<PluginEnvelope>();
        var tasks = new List<Func<Task>>();

        _dependents.Clear();

        foreach (var m in manifests)
        {
            var gate = GetKeyGate(m.Key);

            await gate.WaitAsync(ct).ConfigureAwait(false);

            foreach (var dep in m.Dependencies)
                _dependents.GetOrAdd(dep.Key, _ => []).Add(m.Key);

            var folder = byKey[m.Key];
            LoadedAssembly asm;

            try
            {
                asm = GetLoadedPluginAssembly(m, folder);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load assembly for {Name}@{Version} by {Author}",
                    m.Name,
                    m.Version,
                    m.Author
                );

                gate.Release();

                continue;
            }

            try
            {
                var current = _live.GetValueOrDefault(m.Key);

                if (current is not null)
                {
                    if (_dependents.TryGetValue(m.Key, out var deps) && deps.Any(_live.ContainsKey))
                        throw new InvalidOperationException(
                            $"Cannot reload {m.Key} while dependents are active: {string.Join(",", deps.Where(_live.ContainsKey))}"
                        );

                    await StopAndTearDownAsync(current, ct).ConfigureAwait(false);
                }

                var next = await BuildEnvelopeAsync(asm, m, folder, ct).ConfigureAwait(false);

                _live[m.Key] = next;
                envs.Add(next);

                tasks.Add(async () =>
                {
                    var disp = await _processor
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

        if (unloadRemoved)
            await UnloadRemovedAsync(envs.Select(d => d.Key), ct).ConfigureAwait(false);

        _logger.LogInformation("Loaded {Count} plugins", _live.Count);
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
        LoadedAssembly asm,
        PluginManifest m,
        string folder,
        CancellationToken ct
    )
    {
        var inst = CreatePluginInstance(asm.Assembly);

        if (!string.Equals(inst.Key, m.Key, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Plugin key mismatch: manifest={m.Key} entry={inst.Key}"
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
            ?? throw new InvalidOperationException(
                $"Failed to find ITurboPlugin in assembly '{asm.GetName().Name}'."
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
        await ProcessMigrationsAsync(sp, ct).ConfigureAwait(false);

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
            await AssemblyMemoryLoader
                .UnloadAndWaitAsync(env.Alc, default, ct)
                .ConfigureAwait(false);
    }

    private SemaphoreSlim GetKeyGate(string key) =>
        _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
}
