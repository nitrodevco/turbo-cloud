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
using Turbo.Events;
using Turbo.Messages;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Extensions;

namespace Turbo.Plugins;

public class PluginManager(
    IServiceProvider host,
    EventSystem eventSystem,
    MessageSystem messageSystem,
    ILoggerFactory loggerFactory,
    IOptions<PluginConfig> config
) : IAsyncDisposable
{
    private readonly IServiceProvider _host = host;
    private readonly EventSystem _eventSystem = eventSystem;
    private readonly MessageSystem _messageSystem = messageSystem;
    private readonly ILogger _logger = loggerFactory.CreateLogger(nameof(PluginManager));
    private readonly PluginConfig _config = config.Value;

    private readonly ConcurrentDictionary<string, PluginHandle> _loaded = new(
        StringComparer.Ordinal
    );
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _pluginGates = new(
        StringComparer.Ordinal
    );

    public Task LoadOrReloadAllPlugins(CancellationToken ct = default) =>
        LoadOrReloadAllPlugins(
            maxDegreeOfParallelism: Math.Clamp(Environment.ProcessorCount - 1, 1, 8),
            ct: ct
        );

    public async Task LoadOrReloadAllPlugins(
        int maxDegreeOfParallelism,
        CancellationToken ct = default
    )
    {
        var what = AppContext.BaseDirectory;

        if (!Directory.Exists(_config.PluginFolderPath))
        {
            _logger.LogWarning("Plugin folder does not exist: {Path}", _config.PluginFolderPath);

            return;
        }

        _logger.LogInformation("Loading plugins from {Path} ...", _config.PluginFolderPath);

        var dirs = Directory.EnumerateDirectories(_config.PluginFolderPath).ToArray();

        if (dirs.Length == 0)
        {
            _logger.LogInformation("No plugins found in {Path}.", _config.PluginFolderPath);

            return;
        }

        using var throttler = new SemaphoreSlim(Math.Max(1, maxDegreeOfParallelism));

        var tasks = new List<Task>(dirs.Length);

        foreach (var dir in dirs)
        {
            ct.ThrowIfCancellationRequested();

            tasks.Add(
                Task.Run(
                    async () =>
                    {
                        await throttler.WaitAsync(ct).ConfigureAwait(false);

                        try
                        {
                            await LoadOrReloadAsync(dir, ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        { /* bubble through WhenAll */
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to load plugin from {Dir}", dir);
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    },
                    ct
                )
            );
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task LoadOrReloadAsync(string pluginSourceDir, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var manifest = PluginHelpers.ReadManifest(pluginSourceDir);

        _logger.LogInformation(
            "Loading {PluginId}@{Version} by {Author}",
            manifest.Id,
            manifest.Version,
            manifest.Author
        );

        var gate = _pluginGates.GetOrAdd(manifest.Id, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var shadowDir = PluginHelpers.CreateShadowCopy(pluginSourceDir, manifest.Id);
            var alc = new PluginLoadContext(shadowDir);
            var entryPath = PluginHelpers.FindEntryAssemblyPath(shadowDir, manifest);
            Assembly asm;

            try
            {
                asm = alc.LoadFromAssemblyPath(entryPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load {PluginId}@{Version} by {Author} from {Path}",
                    manifest.Id,
                    manifest.Version,
                    manifest.Author,
                    entryPath
                );
                alc.Unload();
                PluginHelpers.TryDeleteDirectory(shadowDir);

                throw;
            }

            if (_loaded.TryRemove(manifest.Id, out var old))
                await UnloadInternalAsync(old, TimeSpan.FromSeconds(15)).ConfigureAwait(false);

            PluginHandle handle;

            try
            {
                handle = BuildPlugin(alc, asm, manifest, shadowDir, ct);
                await EnablePlugin(handle, ct).ConfigureAwait(false);
            }
            catch
            {
                // ensure cleanup of this attempt
                await UnloadInternalAsync(
                        new PluginHandle
                        {
                            Id = manifest.Id,
                            Version = TryParseVersion(manifest.Version),
                            ShadowDir = shadowDir,
                            Alc = alc,
                            PluginAssembly = asm,
                            ServiceProvider = null!,
                            Plugin = new NoopTurboPlugin(),
                            Disposables = new List<IDisposable>(),
                        },
                        TimeSpan.FromSeconds(10)
                    )
                    .ConfigureAwait(false);
                throw;
            }

            _loaded[manifest.Id] = handle;
            _logger.LogInformation(
                "Loaded {PluginId}@{Version} by {Author}",
                manifest.Id,
                manifest.Version,
                manifest.Author
            );
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task UnloadByIdAsync(string pluginId, TimeSpan timeout)
    {
        var gate = _pluginGates.GetOrAdd(pluginId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_loaded.TryRemove(pluginId, out var h))
                await UnloadInternalAsync(h, timeout).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var h in _loaded.Values)
            await UnloadInternalAsync(h, TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        _loaded.Clear();

        foreach (var gate in _pluginGates.Values)
            gate.Dispose();
        _pluginGates.Clear();
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

    private IServiceProvider CreatePluginServiceProvider(
        ITurboPlugin plugin,
        PluginManifest manifest
    )
    {
        var services = new ServiceCollection();

        services.ConfigurePlugin(_host, plugin, manifest);

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false }
        );
    }

    private async Task EnablePlugin(PluginHandle handle, CancellationToken ct)
    {
        try
        {
            using var scope = handle.ServiceProvider.CreateScope();
            var dbModule = scope.ServiceProvider.GetService<IPluginDbModule>();

            if (dbModule is not null)
                await dbModule.MigrateAsync(scope.ServiceProvider, ct).ConfigureAwait(false);

            var manifest = handle.ServiceProvider.GetRequiredService<PluginManifest>();

            handle.Disposables.Add(
                _eventSystem.RegisterFromAssembly(
                    manifest.Id,
                    handle.PluginAssembly,
                    handle.ServiceProvider,
                    true
                )
            );

            handle.Disposables.Add(
                _messageSystem.RegisterFromAssembly(
                    manifest.Id,
                    handle.PluginAssembly,
                    handle.ServiceProvider,
                    true
                )
            );

            await handle.Plugin.OnEnableAsync(handle.ServiceProvider, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin {Id} enable failed.", handle.Id);

            throw;
        }
    }

    private static Version TryParseVersion(string v) =>
        Version.TryParse(v, out var parsed) ? parsed : new Version(0, 0, 0, 0);

    private PluginHandle BuildPlugin(
        PluginLoadContext alc,
        Assembly asm,
        PluginManifest manifest,
        string shadowDir,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();

        var plugin = CreatePluginInstance(asm, shadowDir);
        var sp = CreatePluginServiceProvider(plugin, manifest);

        return new PluginHandle
        {
            Id = manifest.Id,
            Version = TryParseVersion(manifest.Version),
            ShadowDir = shadowDir,
            Alc = alc,
            PluginAssembly = asm,
            ServiceProvider = sp,
            Plugin = plugin,
            Disposables = new List<IDisposable>(),
        };
    }

    private async Task UnloadInternalAsync(PluginHandle h, TimeSpan timeout)
    {
        try
        {
            await h
                .Plugin.OnDisableAsync(h.ServiceProvider, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plugin {Id} OnDisable failed.", h.Id);
        }

        foreach (var d in h.Disposables)
        {
            try
            {
                d.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin {Id} disposable threw on Dispose().", h.Id);
            }
        }
        h.Disposables.Clear();

        try
        {
            await h.Plugin.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plugin {Id} DisposeAsync failed.", h.Id);
        }

        try
        {
            if (h.ServiceProvider is IAsyncDisposable iad)
                await iad.DisposeAsync().ConfigureAwait(false);
            else
                (h.ServiceProvider as IDisposable)?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plugin {Id} ServiceProvider dispose failed.", h.Id);
        }

        try
        {
            h.Alc.Unload();

            var weak = new WeakReference(h.Alc);
            var start = DateTime.UtcNow;
            do
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                if (!weak.IsAlive)
                    break;
            } while (DateTime.UtcNow - start < timeout);

            if (weak.IsAlive)
                _logger.LogWarning("Plugin {Id} did not unload (leaked references).", h.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plugin {Id} unload failed.", h.Id);
        }

        PluginHelpers.TryDeleteDirectory(h.ShadowDir);
        _logger.LogInformation("Plugin {Id} unloaded.", h.Id);
    }
}
