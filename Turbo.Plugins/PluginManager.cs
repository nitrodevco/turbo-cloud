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
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Database.Delegates;
using Turbo.Logging.Extensions;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Exports;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Plugins;

public sealed class PluginManager(
    IServiceProvider _host,
    IOptions<PluginConfig> config,
    ILogger<PluginManager> logger
)
{
    private readonly IServiceProvider _host = _host;
    private readonly ExportRegistry _exports = new();
    private readonly PluginConfig _config = config.Value;
    private readonly ILogger _logger = logger;

    private readonly ConcurrentDictionary<string, PluginEnvelope> _live = new(
        StringComparer.OrdinalIgnoreCase
    );

    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _dependents = new(
        StringComparer.OrdinalIgnoreCase
    );

    private List<(PluginManifest manifest, string folder)> Discover()
    {
        var list = new List<(PluginManifest, string)>();

        if (!Directory.Exists(_config.PluginFolderPath))
            return list;

        foreach (var dir in Directory.EnumerateDirectories(_config.PluginFolderPath))
        {
            try
            {
                var manifest = PluginDiscovery.ReadManifest(dir);

                list.Add((manifest, dir));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read manifest in {dir}", dir);
            }
        }

        return list;
    }

    public async Task LoadAll(bool unloadRemoved = true, CancellationToken ct = default)
    {
        var discovered = Discover();
        var manifests = PluginDependencyResolver.SortManifests(
            [.. discovered.Select(d => d.manifest)]
        );
        var byKey = discovered.ToDictionary(d => d.manifest.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var manifest in manifests)
        {
            var triplet = byKey[manifest.Key];

            foreach (var d in manifest.Dependencies)
            {
                _dependents.TryAdd(d.Key, []);
                _dependents[d.Key].Add(manifest.Key);
            }

            await LoadOne(manifest, triplet.folder, ct);
        }

        if (unloadRemoved)
        {
            var removed = _live.Keys.Where(k => !byKey.ContainsKey(k)).ToList();

            foreach (var k in removed)
            {
                try
                {
                    await Unload(k, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to unload removed plugin {Key}", k);
                }
            }
        }

        _logger.LogInformation("Loaded {Count} plugins", _live.Count);
    }

    public async Task Reload(string key, CancellationToken ct)
    {
        if (_dependents.TryGetValue(key, out var deps) && deps.Any(_live.ContainsKey))
            throw new InvalidOperationException(
                $"Cannot reload {key} while dependents active: {string.Join(",", deps.Where(_live.ContainsKey))}"
            );

        if (!_live.TryGetValue(key, out var current))
            throw new InvalidOperationException($"Plugin {key} not loaded");

        var manifest = PluginDiscovery.ReadManifest(current.Folder);

        await LoadOne(manifest, current.Folder, ct, key);
    }

    public async Task Unload(string key, CancellationToken ct)
    {
        if (_dependents.TryGetValue(key, out var deps) && deps.Any(_live.ContainsKey))
            throw new InvalidOperationException(
                $"Cannot unload {key}; dependents active: {string.Join(",", deps.Where(_live.ContainsKey))}"
            );

        if (_live.TryRemove(key, out var scope))
        {
            await StopAndTearDown(scope, ct).ConfigureAwait(false);
            _logger.LogInformation("Unloaded {Key}", key);
        }
    }

    private async Task LoadOne(
        PluginManifest manifest,
        string folder,
        CancellationToken ct,
        string? replaceKey = null
    )
    {
        _logger.LogInformation(
            "Loading {Key}@{Version} by {Author}",
            manifest.Key,
            manifest.Version,
            manifest.Author
        );

        var shadowDir = PluginHelpers.CreateShadowCopy(folder, manifest.Key);
        var alc = new PluginLoadContext(shadowDir);
        var asmPath = PluginDiscovery.GetAssemblyPath(shadowDir, manifest);

        try
        {
            var asm = alc.LoadFromAssemblyPath(asmPath);
            var instance = CreatePluginInstance(asm, shadowDir);

            if (!string.Equals(instance.Key, manifest.Key, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Key mismatch manifest={manifest.Key} entry={instance.Key}"
                );

            var sp = CreatePluginServiceProvider(instance, manifest);

            await instance.BindExportsAsync(new ExportBinder(_exports), sp).ConfigureAwait(false);

            var processor = _host.GetRequiredService<AssemblyProcessor>();
            var handle = await processor.ProcessAsync(asm, sp);

            var env = new PluginEnvelope
            {
                Manifest = manifest,
                Folder = folder,
                ALC = alc,
                Assembly = asm,
                Instance = instance,
                Scope = sp,
                Disposables = [handle],
            };

            await EnablePlugin(env, ct).ConfigureAwait(false);

            if (replaceKey is not null || _live.ContainsKey(manifest.Key))
            {
                var k = replaceKey ?? manifest.Key;

                if (_live.TryGetValue(k, out var old))
                {
                    _logger.LogInformation(
                        "Reloading {Key} {OldVer} -> {NewVer}",
                        k,
                        old.Manifest.Version,
                        manifest.Version
                    );

                    _live[k] = env;
                    await StopAndTearDown(old, ct).ConfigureAwait(false);
                }
                else
                {
                    _live[manifest.Key] = env;
                }
            }
            else
            {
                _live[manifest.Key] = env;
            }

            _logger.LogInformation(
                "Loaded {Key}@{Version} by {Author}",
                manifest.Key,
                manifest.Version,
                manifest.Author
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load {Name}@{Version} by {Author} from {Path}",
                manifest.Name,
                manifest.Version,
                manifest.Author,
                asmPath
            );

            alc.Unload();

            throw;
        }
    }

    private async Task EnablePlugin(PluginEnvelope envelope, CancellationToken ct)
    {
        var sp = envelope.Scope;

        using (IServiceScope scope = sp.CreateScope())
        {
            var dbModule = scope.ServiceProvider.GetService<IPluginDbModule>();

            if (dbModule is not null)
                await dbModule.MigrateAsync(scope.ServiceProvider, ct).ConfigureAwait(false);
        }

        envelope.Instance.StartAsync(sp, ct).GetAwaiter().GetResult();
    }

    private ITurboPlugin CreatePluginInstance(Assembly asm, string shadowDir)
    {
        try
        {
            var types = PluginHelpers.SafeGetLoadableTypes(asm);
            var pluginType =
                types.First(t => !t.IsAbstract && typeof(ITurboPlugin).IsAssignableFrom(t))
                ?? throw new InvalidOperationException($"No ITurboPlugin found in {shadowDir}.");
            return (ITurboPlugin)Activator.CreateInstance(pluginType)!;
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var le in ex.LoaderExceptions.Where(e => e is not null))
            {
                if (le is FileNotFoundException fnf)
                    _logger.LogError("Plugin: missing dependency: {Message}", fnf.Message);
                else
                    _logger.LogError("Plugin: type load error: {Message}", le!.Message);
            }
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

        services.AddSingleton<TablePrefixProvider>(sp =>
        {
            var manifest = sp.GetRequiredService<PluginManifest>();

            return () => manifest.TablePrefix ?? string.Empty;
        });

        plugin.ConfigureServices(services, manifest);

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false }
        );
    }

    private async Task StopAndTearDown(PluginEnvelope env, CancellationToken ct)
    {
        try
        {
            if (env.Disposables.Count > 0)
            {
                foreach (var inst in env.Disposables)
                {
                    try
                    {
                        if (inst is IAsyncDisposable iad)
                            await iad.DisposeAsync().ConfigureAwait(false);
                        else if (inst is IDisposable d)
                            d.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Exception disposing plugin resource for {Key}",
                            env.Manifest.Key
                        );
                    }
                }

                env.Disposables.Clear();
            }

            try
            {
                await env.Instance.StopAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StopAsync threw for {Key}", env.Manifest.Key);
            }

            try
            {
                env.Scope.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing plugin scope for {Key}", env.Manifest.Key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "StopAndTearDown failed for {Key}", env.Manifest.Key);
        }

        env.ALC.Unload();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
