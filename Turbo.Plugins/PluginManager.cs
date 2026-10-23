using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Admin;
using Turbo.Database.Migrations;
using Turbo.Logging.Extensions;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Exceptions;
using Turbo.Plugins.Exports;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Settings;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Runtime.Configuration;

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

    // How each plugin found stands, and the folders whose manifest could not be read, as of the
    // last scan or action: what the admin panel lists.
    private readonly ConcurrentDictionary<string, PluginStatus> _status = new(
        StringComparer.OrdinalIgnoreCase
    );
    private ImmutableArray<(string Folder, string Error)> _unreadable = [];

    internal PluginStateFile State { get; } = new(config.Value.PluginFolderPath);

    internal PluginConfig Config => _config;

    /// <summary>The admin endpoints and pages of the live plugins; none in a host without them.</summary>
    private PluginAdminRoutes? Routes => host.GetService<PluginAdminRoutes>();

    private static readonly ServiceProviderOptions SP_OPTIONS = new()
    {
        ValidateScopes = true,
        ValidateOnBuild = false,
    };

    private List<(PluginManifest manifest, string folder)> DiscoverPlugins()
    {
        var list = new List<(PluginManifest, string)>(capacity: 16);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unreadable = new List<(string, string)>();

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
                unreadable.Add((dir, ex.Message));
                _logger.LogError(ex, "Failed to read dev plugin manifest in {Dir}", dir);
            }
        }

        if (!Directory.Exists(_config.PluginFolderPath))
        {
            _unreadable = [.. unreadable];

            return list;
        }

        {
            foreach (var dir in Directory.EnumerateDirectories(_config.PluginFolderPath))
            {
                // Dot-folders are the server's own (".versions": earlier versions kept to roll back to).
                if (Path.GetFileName(dir).StartsWith('.'))
                    continue;

                try
                {
                    var manifest = PluginHelpers.ReadManifest(dir);

                    if (seen.Add(manifest.Key))
                        list.Add((manifest, dir));
                }
                catch (Exception ex)
                {
                    unreadable.Add((dir, ex.Message));
                    _logger.LogError(ex, "Failed to read plugin manifest in {Dir}", dir);
                }
            }
        }

        _unreadable = [.. unreadable];

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
            var disabled = ReadDisabled();

            RebuildDependents(plugins.Select(p => p.Manifest));
            ForgetGone(plugins);

            foreach (var (m, folder) in plugins)
            {
                // A plugin turned off is left out like one removed: unloaded below if it is live.
                if (disabled.Contains(m.Key))
                {
                    SetStatus(m, folder, PluginStateType.Disabled, null);

                    continue;
                }

                var gate = GetKeyGate(m.Key);

                await gate.WaitAsync(ct).ConfigureAwait(false);

                try
                {
                    await LoadAndProcessAsync(m, folder, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (MigrationException.FindIn(ex) is { } migration)
                {
                    // Its tables cannot be brought up to date (a database from a newer version,
                    // a destructive migration not allowed, migrations set to Check). The message
                    // says what to do; the server carries on without the plugin.
                    SetStatus(m, folder, PluginStateType.Failed, migration.Message);
                    _logger.LogError(
                        "Not loading {Name}@{Version} by {Author}: {Message}",
                        m.Name,
                        m.Version,
                        m.Author,
                        migration.Message
                    );
                }
                catch (Exception ex)
                {
                    SetStatus(m, folder, PluginStateType.Failed, ex.Message);
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

            // Only a plugin whose folder is gone counts as removed. One that failed to reload is
            // still on disk and keeps its previous live version.
            if (unloadRemoved)
                await UnloadRemovedAsync(
                        plugins.Select(p => p.Manifest.Key).Where(k => !disabled.Contains(k)),
                        ct
                    )
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
                _status.TryRemove(key, out _);
                _logger.LogInformation("Plugin {Key} was removed from disk and unloaded.", key);
                return;
            }

            if (ReadDisabled().Contains(key))
            {
                await UnloadAsync(key, ct).ConfigureAwait(false);
                SetStatus(match.Manifest, match.Folder, PluginStateType.Disabled, null);
                _logger.LogInformation("Plugin {Key} is turned off, so it was not loaded.", key);
                return;
            }

            var gate = GetKeyGate(key);
            await gate.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                await LoadAndProcessAsync(match.Manifest, match.Folder, ct).ConfigureAwait(false);

                _logger.LogInformation("Reloaded plugin {Key}", key);
            }
            catch (Exception ex)
            {
                SetStatus(
                    match.Manifest,
                    match.Folder,
                    PluginStateType.Failed,
                    MigrationException.FindIn(ex)?.Message ?? ex.Message
                );

                throw;
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
    /// Loads the plugin, then registers what its assembly declares (commands, permission nodes,
    /// handlers, room logic...). When that fails (a class whose services can't be resolved), the
    /// plugin is stopped and taken out again before the exception goes on: one plugin that can't
    /// load leaves nothing of itself behind, and the server and the other plugins carry on. The
    /// caller holds the plugin's key gate.
    /// </summary>
    private async Task LoadAndProcessAsync(PluginManifest m, string folder, CancellationToken ct)
    {
        var (asm, next) = await LoadPluginAsync(m, folder, ct).ConfigureAwait(false);

        try
        {
            var disp = await processor
                .ProcessAsync(asm.Assembly, next.ServiceProvider, ct)
                .ConfigureAwait(false);

            next.Disposables.Add(disp);
        }
        catch
        {
            _live.TryRemove(new KeyValuePair<string, PluginEnvelope>(m.Key, next));

            var unloading = await StopAndTearDownAsync(next, ct).ConfigureAwait(false);

            next = null!;
            asm = null!;
            await WaitForUnloadAsync(unloading, m.Key, ct).ConfigureAwait(false);

            throw;
        }
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
        // A folder copied in by hand is held to the API as an installed package is.
        if (PluginApi.Refusal(m) is { } refusal)
            throw new InvalidOperationException(refusal);

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
        WeakReference? unloading = null;

        if (current is not null)
        {
            // Until the new version is up, nothing of the plugin is registered: not "loaded".
            Routes?.Reloading(m.Key);

            if (_status.TryGetValue(m.Key, out var was))
                _status[m.Key] = was with
                {
                    State = PluginStateType.NotLoaded,
                    LoadedAtUtc = null,
                    Migrations = [],
                };

            // Torn down, it is no longer live: a new version that fails to build leaves the
            // plugin out rather than a stopped one in the live map.
            _live.TryRemove(m.Key, out _);
            unloading = await StopAndTearDownAsync(current, ct).ConfigureAwait(false);
            current = null;
        }

        var next = await BuildEnvelopeAsync(asm, m, folder, ct).ConfigureAwait(false);

        _live[m.Key] = next;

        await WaitForUnloadAsync(unloading, m.Key, ct).ConfigureAwait(false);

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
                var unloading = await StopAndTearDownAsync(env, ct).ConfigureAwait(false);

                env = null!;
                await WaitForUnloadAsync(unloading, key, ct).ConfigureAwait(false);

                if (
                    _status.TryGetValue(key, out var status)
                    && status.State == PluginStateType.Loaded
                )
                    _status[key] = status with
                    {
                        State = PluginStateType.NotLoaded,
                        LoadedAtUtc = null,
                        Migrations = [],
                    };

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

        var extensions = new PluginExtensions();
        var clientModules = host.GetService<IClientModuleDirectory>() is { } directory
            ? new PluginClientModules(directory, m, folder)
            : null;
        var (sp, contexts, settings) = CreatePluginServiceProvider(
            inst,
            m,
            extensions,
            clientModules
        );
        var disposables = new List<object> { sp };

        // Its client module goes when it stops, whether or not it took it away itself.
        if (clientModules is not null)
            disposables.Add(clientModules);

        try
        {
            // Its settings are listed while it is live, from before it starts reading them.
            if (settings.Count > 0 && host.GetService<IPluginSettingSections>() is { } sections)
                disposables.Insert(
                    0,
                    sections.Add(
                        m.Key,
                        settings,
                        type => RunningOptions(sp, type),
                        Path.ChangeExtension(PluginHelpers.GetAssemblyPath(folder, m), ".xml")
                    )
                );

            // Its admin nodes are registered before it starts; its endpoints and pages go live
            // once it has, and are taken away first when it stops.
            var admin = sp.GetService<IPluginAdmin>() is { } extension
                ? PluginAdminRoutes.Build(m.Key, m.Name, m.Version, extension)
                : null;

            if (admin is not null && host.GetService<IPermissionRegistryProvider>() is { } nodes)
                disposables.Insert(0, nodes.Register(admin.Nodes));

            // Its files are served, and its own variables and texts sent, while it is loaded.
            clientModules?.ServeFiles();

            if (await SeedGamedataAsync(m, folder, ct).ConfigureAwait(false) is { } loaded)
                disposables.Add(loaded);

            await inst.BindExportsAsync(new ExportBinder(_exports), sp).ConfigureAwait(false);
            await StartPluginAsync(inst, sp, ct).ConfigureAwait(false);

            if (admin is not null && Routes is { } routes)
                disposables.Insert(0, routes.Live(m.Key, admin));
        }
        catch
        {
            foreach (var disposable in disposables.OfType<IDisposable>())
                disposable.Dispose();

            throw;
        }

        _status[m.Key] = new PluginStatus(
            m,
            folder,
            PluginStateType.Loaded,
            null,
            await ReadMigrationsAsync(sp, contexts, ct).ConfigureAwait(false),
            DateTime.UtcNow
        );

        return new PluginEnvelope
        {
            Extensions = extensions,
            Key = m.Key,
            Assembly = asm.Assembly,
            Alc = asm.Alc,
            Manifest = m,
            Folder = folder,
            Instance = inst,
            ServiceProvider = sp,
            Disposables = disposables,
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

    /// <summary>
    /// The plugin's own container, the database contexts it registered, whose applied migrations
    /// the admin panel lists, and the config classes it binds, whose settings the panel shows. The
    /// host's configuration is in the container, so a plugin binds a class to its section as a
    /// module does (<c>services.AddOptions&lt;T&gt;().BindConfiguration(T.SECTION_NAME)</c>).
    /// </summary>
    private (
        ServiceProvider Services,
        List<Type> Contexts,
        List<Type> Settings
    ) CreatePluginServiceProvider(
        ITurboPlugin plugin,
        PluginManifest manifest,
        PluginExtensions extensions,
        PluginClientModules? clientModules
    )
    {
        var services = new ServiceCollection();

        services.AddSingleton(manifest);
        services.AddSingleton<IPluginCatalog>(new PluginCatalog(_exports));
        services.AddSingleton<IHostServices>(new HostServices(host, extensions, clientModules));
        services.ConfigurePrefixedLogging(host, manifest.Name);

        if (host.GetService<IConfiguration>() is { } configuration)
        {
            services.AddSingleton(configuration);
            services.AddWholeCollectionBinding();
        }

        plugin.ConfigureServices(services, manifest);

        var contexts = services
            .Select(d => d.ServiceType)
            .Where(t => typeof(DbContext).IsAssignableFrom(t))
            .Distinct()
            .ToList();

        var settings = services
            .Select(d => d.ServiceType)
            .Where(t =>
                t.IsGenericType
                && t.GetGenericTypeDefinition() == typeof(IOptionsChangeTokenSource<>)
            )
            .Select(t => t.GetGenericArguments()[0])
            .Distinct()
            .ToList();

        return (services.BuildServiceProvider(SP_OPTIONS), contexts, settings);
    }

    /// <summary>
    /// The plugin's value of a config class now (<see cref="IOptionsMonitor{TOptions}"/>, which
    /// follows the configuration); null when it can't be read.
    /// </summary>
    private object? RunningOptions(IServiceProvider sp, Type type)
    {
        try
        {
            var monitor = sp.GetService(typeof(IOptionsMonitor<>).MakeGenericType(type));

            return monitor
                ?.GetType()
                .GetProperty(nameof(IOptionsMonitor<object>.CurrentValue))
                ?.GetValue(monitor);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read a plugin's {Type} settings", type.Name);

            return null;
        }
    }

    /// <summary>
    /// The migrations applied for the plugin's tables, by name. Read after its own migrations
    /// ran; a database that cannot be asked leaves the list empty rather than failing the load.
    /// </summary>
    private async Task<ImmutableArray<string>> ReadMigrationsAsync(
        IServiceProvider sp,
        List<Type> contexts,
        CancellationToken ct
    )
    {
        if (contexts.Count == 0)
            return [];

        var names = new List<string>();
        var scope = sp.CreateAsyncScope();

        try
        {
            foreach (var type in contexts)
            {
                var db = (DbContext)scope.ServiceProvider.GetRequiredService(type);

                names.AddRange(
                    await db.Database.GetAppliedMigrationsAsync(ct).ConfigureAwait(false)
                );
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not read the applied migrations of a plugin");
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }

        return [.. names];
    }

    /// <summary>
    /// Every plugin found by the last scan or action, live ones with what they registered, then
    /// the folders whose manifest could not be read.
    /// </summary>
    internal ImmutableArray<PluginSnapshot> Snapshot()
    {
        var list = new List<PluginSnapshot>();

        foreach (
            var status in _status.Values.OrderBy(
                s => s.Manifest.Name,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            var m = status.Manifest;
            var extensions = _live.TryGetValue(m.Key, out var env) ? env.Extensions : null;

            list.Add(
                new PluginSnapshot(
                    m.Key,
                    m.Name,
                    m.Version,
                    m.Author,
                    FolderName(status.Folder),
                    IsDev(status.Folder),
                    status.State,
                    status.Error,
                    [.. m.Dependencies.Select(d => d.Key)],
                    extensions?.Capabilities ?? [],
                    extensions?.IncomingHeaders ?? [],
                    extensions?.OutgoingHeaders ?? [],
                    status.Migrations,
                    status.LoadedAtUtc
                )
            );
        }

        foreach (var (folder, error) in _unreadable)
            list.Add(
                new PluginSnapshot(
                    Path.GetFileName(folder),
                    null,
                    null,
                    null,
                    FolderName(folder),
                    IsDev(folder),
                    PluginStateType.Failed,
                    error,
                    [],
                    [],
                    [],
                    [],
                    [],
                    null
                )
            );

        return [.. list];
    }

    /// <summary>The live plugin's folder, or where a known plugin with the key lies.</summary>
    internal string? FolderOf(string key) =>
        _status.TryGetValue(key, out var status) ? status.Folder : null;

    internal bool IsDev(string folder) =>
        !Path.GetFullPath(folder)
            .StartsWith(
                Path.GetFullPath(_config.PluginFolderPath).TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            );

    private string FolderName(string folder) =>
        IsDev(folder) ? Path.GetFullPath(folder) : Path.GetFileName(folder);

    /// <summary>
    /// Scans the folders again so a plugin installed or changed since the
    /// last scan is known, without loading anything.
    /// </summary>
    internal void Refresh()
    {
        var plugins = DiscoverPlugins();
        var disabled = ReadDisabled();

        ForgetGone(plugins);

        foreach (var (m, folder) in plugins)
        {
            if (_live.ContainsKey(m.Key) && !disabled.Contains(m.Key))
                continue;

            SetStatus(
                m,
                folder,
                disabled.Contains(m.Key) ? PluginStateType.Disabled
                    : _status.TryGetValue(m.Key, out var known) ? known.State
                    : PluginStateType.NotLoaded,
                _status.TryGetValue(m.Key, out var was) ? was.Error : null
            );
        }
    }

    private ImmutableHashSet<string> ReadDisabled()
    {
        try
        {
            return State.Disabled;
        }
        catch (Exception ex)
        {
            // A broken file turns nothing off rather than stopping every plugin from loading.
            _logger.LogError(ex, "Could not read {File}; no plugin is turned off", State.Path);

            return ImmutableHashSet<string>.Empty;
        }
    }

    private void SetStatus(PluginManifest m, string folder, PluginStateType state, string? error)
    {
        // Turned off or failed: what it served in the panel answers "off" until it is back.
        if (state != PluginStateType.Loaded)
            Routes?.Off(m.Key);

        _status[m.Key] = new PluginStatus(m, folder, state, error, [], null);
    }

    /// <summary>Forgets plugins whose folder is gone and that are not live.</summary>
    private void ForgetGone(IEnumerable<(PluginManifest Manifest, string Folder)> plugins)
    {
        var found = plugins.Select(p => p.Manifest.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in _status.Keys)
            if (!found.Contains(key) && !_live.ContainsKey(key))
                _status.TryRemove(key, out _);
    }

    /// <summary>
    /// Unloads the plugin, refused while a live plugin depends on it. The caller has turned it
    /// off already, so the next scan leaves it out too.
    /// </summary>
    internal async Task DisableAsync(string key, CancellationToken ct)
    {
        await _reloadGate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            await UnloadAsync(key, ct).ConfigureAwait(false);

            if (_status.TryGetValue(key, out var status))
                _status[key] = status with
                {
                    State = PluginStateType.Disabled,
                    Error = null,
                    LoadedAtUtc = null,
                    Migrations = [],
                };
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    /// <summary>
    /// Unloads the plugin and deletes its folder, holding the gate every load and scan holds, so
    /// none brings it back halfway. False, changing nothing, while a load, reload or scan runs.
    /// Throws <see cref="PluginDependencyException"/> while a live plugin depends on it.
    /// </summary>
    internal async Task<bool> TryRemoveAsync(string key, CancellationToken ct)
    {
        if (!await _reloadGate.WaitAsync(0, ct).ConfigureAwait(false))
            return false;

        try
        {
            await UnloadAsync(key, ct).ConfigureAwait(false);

            Routes?.Off(key);

            if (_status.TryRemove(key, out var status) && Directory.Exists(status.Folder))
                Directory.Delete(status.Folder, recursive: true);

            _logger.LogInformation("Plugin {Key} was uninstalled.", key);

            return true;
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    /// <summary>The manifests of the plugins known, as of the last scan.</summary>
    internal IReadOnlyList<PluginManifest> Manifests() =>
        [.. _status.Values.Select(x => x.Manifest)];

    /// <summary>The dependents of <paramref name="key"/> that are live now.</summary>
    internal IReadOnlyList<string> LiveDependentsOf(string key) =>
        _dependents.TryGetValue(key, out var deps)
            ? [.. deps.Where(_live.ContainsKey).Distinct()]
            : [];

    /// <summary>
    /// Gives the hotel the external variables and texts the plugin brings that it has none of,
    /// before the plugin starts and reads them, and follows the ones it owns to its new values. A
    /// failure is logged; the plugin loads either way. Disposing what it answers stops sending the
    /// plugin's own to clients, as it unloads.
    /// </summary>
    private async Task<IDisposable?> SeedGamedataAsync(
        PluginManifest m,
        string folder,
        CancellationToken ct
    )
    {
        if (host.GetService<IPluginGamedataSeeds>() is not { } seeds)
            return null;

        try
        {
            await seeds.SeedAsync(m.Key, folder, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Adding the gamedata the plugin {Key} brings failed", m.Key);
        }

        return seeds.Loaded(m.Key);
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

    /// <summary>
    /// Stops the plugin, disposes what it registered and starts unloading its context; answers a
    /// weak reference to the context for <see cref="WaitForUnloadAsync"/>, which the caller awaits
    /// only once it holds nothing of the plugin itself (its envelope, the live entry).
    /// </summary>
    private async Task<WeakReference?> StopAndTearDownAsync(
        PluginEnvelope env,
        CancellationToken ct
    )
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

        return env.Alc is null ? null : AssemblyMemoryLoader.BeginUnload(env.Alc);
    }

    /// <summary>
    /// Waits for a plugin's context to be collected, and says so when something outside the
    /// plugin still holds it or a type loaded in it (a registration it did not dispose).
    /// </summary>
    private async Task WaitForUnloadAsync(WeakReference? alc, string key, CancellationToken ct)
    {
        if (alc is null)
            return;

        if (!await AssemblyMemoryLoader.WaitForUnloadAsync(alc, 5000, ct).ConfigureAwait(false))
            _logger.LogWarning(
                "ALC for plugin {Key} did not unload within timeout. Possible memory leak from retained type references.",
                key
            );
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
