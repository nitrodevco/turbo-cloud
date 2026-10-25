using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Hotel;
using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Admin;
using Turbo.Contracts.Plugins.Web;
using Turbo.Database.Context;
using Turbo.Database.Migrations;
using Turbo.Logging.Extensions;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Exceptions;
using Turbo.Plugins.Exports;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Settings;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Runtime.Configuration;
using DatabaseServices = Turbo.Database.Extensions.ServiceCollectionExtensions;

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

    // The host's services behind a plugin's own: never asked for the plugin's types.
    private readonly IServiceProvider _hostForPlugins = new PluginHostServiceProvider(host);

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

    // The waits for the contexts of failed loads. The failure's error, thrown in the plugin's own
    // code, holds its context for as long as anyone holds the error (the load reporting it, the
    // panel showing it), so the wait runs beside the load rather than in its way.
    private readonly ConcurrentDictionary<Task, byte> _failedUnloads = new();

    // How each plugin found stands, and the folders whose manifest could not be read, as of the
    // last scan or action: what the admin panel lists.
    private readonly ConcurrentDictionary<string, PluginStatus> _status = new(
        StringComparer.OrdinalIgnoreCase
    );
    private ImmutableArray<(string Folder, string Error)> _unreadable = [];

    // What each plugin's last load registered, kept after a failed one: the inspector's list.
    // Names only, never the plugin's types, so it holds no unloaded context.
    private readonly ConcurrentDictionary<string, PluginContributionLog> _contributions = new(
        StringComparer.OrdinalIgnoreCase
    );

    internal PluginStateFile State { get; } = new(config.Value.PluginFolderPath);

    internal PluginConfig Config => _config;

    /// <summary>The admin endpoints and pages of the live plugins; none in a host without them.</summary>
    private PluginAdminRoutes? Routes => host.GetService<PluginAdminRoutes>();

    private PluginWebRoutes? WebRoutes => host.GetService<PluginWebRoutes>();

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
    /// Loads every discovered plugin in dependency order, replacing live ones: the live dependents
    /// of each are unloaded first and load again after it. A plugin that fails is logged and
    /// skipped so the rest still load; with <paramref name="unloadRemoved"/>, live plugins whose
    /// folder is gone are unloaded.
    /// </summary>
    public Task LoadAllAsync(bool unloadRemoved = true, CancellationToken ct = default) =>
        ScanAsync(unloadRemoved, changedOnly: false, ct);

    /// <summary>
    /// Loads the plugins that are new, whose code changed on disk since they loaded, or that are
    /// not live (failed, or waiting for a dependency), each with its dependents, and unloads the
    /// live ones whose folder is gone. A live plugin that did not change, and none of whose
    /// dependencies did, keeps running untouched. A plugin that fails is logged and skipped.
    /// </summary>
    public Task LoadChangedAsync(CancellationToken ct = default) =>
        ScanAsync(unloadRemoved: true, changedOnly: true, ct);

    private async Task ScanAsync(bool unloadRemoved, bool changedOnly, CancellationToken ct)
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

                // Its dependents come later in the order: one whose dependency was replaced was
                // unloaded with it, so it is not live here and loads again.
                if (changedOnly && IsCurrent(m.Key, folder))
                    continue;

                await TryLoadAsync(m, folder, plugins, ct).ConfigureAwait(false);
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
    /// Reloads one plugin, or unloads it when its folder is gone, and loads again the plugins that
    /// depend on it (they were bound to the version replaced); the other plugins keep running.
    /// Unlike <see cref="LoadAllAsync"/>, a failure of this plugin is thrown to the caller, who
    /// asked for it; a dependent that fails is marked Failed and logged.
    /// </summary>
    public async Task ReloadAsync(string key, CancellationToken ct = default)
    {
        await _reloadGate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var plugins = DiscoverSorted();
            var disabled = ReadDisabled();

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

            if (disabled.Contains(key))
            {
                await UnloadAsync(key, ct).ConfigureAwait(false);
                SetStatus(match.Manifest, match.Folder, PluginStateType.Disabled, null);
                _logger.LogInformation("Plugin {Key} is turned off, so it was not loaded.", key);
                return;
            }

            var failure = await TryLoadAsync(match.Manifest, match.Folder, plugins, ct)
                .ConfigureAwait(false);

            if (failure is null)
                _logger.LogInformation("Reloaded plugin {Key}", key);

            foreach (var (m, folder) in DependentsOf(key, plugins))
            {
                if (!disabled.Contains(m.Key))
                    await TryLoadAsync(m, folder, plugins, ct).ConfigureAwait(false);
            }

            if (failure is not null)
                ExceptionDispatchInfo.Throw(failure);
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    /// <summary>
    /// Loads or replaces one plugin, holding its key gate. Its live dependents are unloaded first
    /// (dependents of dependents before them); the caller loads them again after it. A failure
    /// marks the plugin Failed with the reason the panel shows, is logged, and is answered rather
    /// than thrown.
    /// </summary>
    private async Task<Exception?> TryLoadAsync(
        PluginManifest m,
        string folder,
        IReadOnlyList<(PluginManifest Manifest, string Folder)> plugins,
        CancellationToken ct
    )
    {
        var gate = GetKeyGate(m.Key);

        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            foreach (var (dependent, _) in Enumerable.Reverse(DependentsOf(m.Key, plugins)))
            {
                if (_live.ContainsKey(dependent.Key))
                    await UnloadAsync(dependent.Key, ct).ConfigureAwait(false);
            }

            await LoadAndProcessAsync(m, folder, ct).ConfigureAwait(false);

            return null;
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

            return ex;
        }
        catch (Exception ex)
        {
            var reason = PluginHelpers.LoadFailure(ex);

            SetStatus(m, folder, PluginStateType.Failed, reason);
            _logger.LogError(
                ex,
                "Failed to load {Name}@{Version} by {Author}: {Reason}",
                m.Name,
                m.Version,
                m.Author,
                reason
            );

            return ex;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// The plugins on disk that depend on <paramref name="key"/>, directly or through another, in
    /// load order.
    /// </summary>
    private List<(PluginManifest Manifest, string Folder)> DependentsOf(
        string key,
        IReadOnlyList<(PluginManifest Manifest, string Folder)> plugins
    )
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>([key]);

        while (queue.TryDequeue(out var next))
        {
            if (!_dependents.TryGetValue(next, out var deps))
                continue;

            foreach (var dep in deps)
            {
                if (found.Add(dep))
                    queue.Enqueue(dep);
            }
        }

        return [.. plugins.Where(p => found.Contains(p.Manifest.Key))];
    }

    /// <summary>The live version of the plugin was loaded from this folder as its code is now.</summary>
    private bool IsCurrent(string key, string folder) =>
        _live.TryGetValue(key, out var live)
        && string.Equals(live.Folder, folder, StringComparison.OrdinalIgnoreCase)
        && live.Fingerprint == PluginHelpers.CodeFingerprint(folder);

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
    /// A version it replaces is waited for only after that: clients are sent the new version's
    /// module as it starts, so its handlers must not wait on the old assembly being collected,
    /// which a reference left behind holds up for the whole unload timeout.
    /// </summary>
    private async Task LoadAndProcessAsync(PluginManifest m, string folder, CancellationToken ct)
    {
        var (asm, next, replaced) = await LoadPluginAsync(m, folder, ct).ConfigureAwait(false);

        try
        {
            // Its classes take the plugin's own services first, then the host's, as its packet
            // handlers and room object logic always did: a command or permission source may take
            // IGrainFactory, ISessionGateway... by constructor as core's do.
            var disp = await processor
                .ProcessAsync(
                    asm.Assembly,
                    new CompositeServiceProvider(next.ServiceProvider, _hostForPlugins),
                    ct
                )
                .ConfigureAwait(false);

            next.Disposables.Add(disp);
            WhenHotelReady(next);
        }
        catch
        {
            _live.TryRemove(new KeyValuePair<string, PluginEnvelope>(m.Key, next));

            var unloading = await StopAndTearDownAsync(next, ct).ConfigureAwait(false);

            next = null!;
            asm = null!;
            WaitForFailedUnload(unloading, m.Key);

            throw;
        }
        finally
        {
            await WaitForUnloadAsync(replaced, m.Key, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Replaces the live version of one plugin with a fresh load of <paramref name="folder"/>.
    /// The caller holds the plugin's key gate, runs feature processing on the result and then
    /// waits for the replaced version's context (`Replaced`) to unload. Refuses while a
    /// dependency is not live, and refuses to replace a live version that live dependents are
    /// bound to.
    /// </summary>
    private async Task<(
        LoadedAssembly Assembly,
        PluginEnvelope Envelope,
        WeakReference? Replaced
    )> LoadPluginAsync(PluginManifest m, string folder, CancellationToken ct)
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

        // Checks come first so a refused load does not leave an assembly context behind. Its code
        // is fingerprinted before it is read: a change while it loads is seen by the next scan.
        var fingerprint = PluginHelpers.CodeFingerprint(folder);
        var asm = GetLoadedPluginAssembly(m, folder);
        WeakReference? unloading = null;

        if (current is not null)
        {
            // Until the new version is up, nothing of the plugin is registered: not "loaded".
            Routes?.Reloading(m.Key);
            WebRoutes?.Reloading(m.Key);

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

        PluginEnvelope next;

        try
        {
            next = await BuildEnvelopeAsync(asm, m, folder, fingerprint, ct).ConfigureAwait(false);
        }
        catch
        {
            // It never went live, so nothing else would unload its context: it stayed loaded.
            var failed = AssemblyMemoryLoader.BeginUnload(asm.Alc);

            asm = null!;
            WaitForFailedUnload(failed, m.Key);

            throw;
        }

        _live[m.Key] = next;

        return (asm, next, unloading);
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
                // Its web routes stop answering before it stops, not once it has.
                WebRoutes?.Off(key);

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

    /// <summary>
    /// Unloads every live plugin, as the server stops: a plugin after the plugins that depend on
    /// it, so each is stopped while what it depends on still runs.
    /// </summary>
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
            // The plugins no live plugin depends on any more. A dependency taken first was refused
            // ("dependents active") and left running.
            var leafs = keys.Where(k =>
                    !dependents.TryGetValue(k, out var list) || !list.Any(keys.Contains)
                )
                .ToList();

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

        await Task.WhenAll(_failedUnloads.Keys).ConfigureAwait(false);
    }

    private static LoadedAssembly GetLoadedPluginAssembly(PluginManifest manifest, string folder)
    {
        // A data plugin: its files are all it has.
        if (PluginHelpers.IsDataOnly(manifest))
            return AssemblyMemoryLoader.Empty(folder, $"TurboDataPlugin.{manifest.Key}");

        var asmPath = PluginHelpers.GetAssemblyPath(folder, manifest);

        return AssemblyMemoryLoader.LoadFromBytes(asmPath);
    }

    private async Task<PluginEnvelope> BuildEnvelopeAsync(
        LoadedAssembly asm,
        PluginManifest m,
        string folder,
        string fingerprint,
        CancellationToken ct
    )
    {
        var inst = CreatePluginInstance(asm.Assembly, m);

        if (!string.Equals(inst.Key, m.Key, StringComparison.Ordinal))
            throw new PluginAssemblyException(
                PluginAssemblyErrorType.KeyMismatch,
                m.Key,
                entryPointKey: inst.Key
            );

        var extensions = new PluginExtensions();
        var contributions = new PluginContributionLog();

        _contributions[m.Key] = contributions;

        var clientModules = host.GetService<IClientModuleDirectory>() is { } directory
            ? new PluginClientModules(directory, m, folder, contributions)
            : null;
        var (sp, contexts, settings, replaces) = CreatePluginServiceProvider(
            inst,
            asm.Assembly,
            m,
            extensions,
            clientModules,
            contributions
        );
        var disposables = new List<object> { sp };
        var started = new List<IHostedService>();

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

            // Its web routes are built (and a bad one refused) before it starts, and answer once it has.
            var web = WebRoutes is not null
                ? PluginWebRoutes.Build(
                    m.Key,
                    asm.Assembly,
                    new CompositeServiceProvider(sp, _hostForPlugins)
                )
                : [];

            if (admin is not null && host.GetService<IPermissionRegistryProvider>() is { } nodes)
                disposables.Insert(0, nodes.Register(admin.Nodes));

            NoteContributions(contributions, folder, contexts, settings, admin, web);

            // The host services it replaces are built before it starts (one that can't be fails
            // the load) and go in their slots once it has; they are taken out first when it stops.
            var replacements = replaces
                .Select(type => (Type: type, Implementation: sp.GetRequiredService(type)))
                .ToList();

            foreach (var (type, implementation) in replacements)
                contributions.Add(
                    PluginContributionKinds.SERVICE,
                    type.Name,
                    implementation.GetType().Name
                );

            // Its files are served, and its own variables and texts sent, while it is loaded.
            clientModules?.ServeFiles();

            if (await SeedGamedataAsync(m, folder, ct).ConfigureAwait(false) is { } loaded)
                disposables.Add(loaded);

            await inst.BindExportsAsync(new ExportBinder(_exports), sp).ConfigureAwait(false);
            await StartPluginAsync(inst, sp, started, ct).ConfigureAwait(false);

            foreach (var service in started)
                contributions.Add(
                    PluginContributionKinds.HOSTED_SERVICE,
                    service.GetType().FullName ?? service.GetType().Name
                );

            // Its wwwroot/<key>.js is its client module, unless it registered one itself; a data
            // plugin runs no code, in the client either.
            if (
                !PluginHelpers.IsDataOnly(m)
                && clientModules?.RegisterConventionalModule() is { } module
            )
                _logger.LogInformation(
                    "Plugin {Key} runs the client module {Module}",
                    m.Key,
                    module
                );

            if (admin is not null && Routes is { } routes)
                disposables.Insert(0, routes.Live(m.Key, admin));

            if (web.Length > 0 && WebRoutes is { } webRoutes)
                disposables.Insert(0, webRoutes.Live(m.Key, web));

            if (host.GetService<IReplaceableServices>() is { } replaceable)
                foreach (var (type, implementation) in replacements)
                    disposables.Insert(0, replaceable.Replace(type, m.Key, implementation));
        }
        catch
        {
            // Its hosted services started before it failed run plugin code (timers, loops)
            // until stopped.
            await StopHostedServicesAsync(started, m.Key, ct).ConfigureAwait(false);
            await DisposeAllAsync(disposables, m.Key).ConfigureAwait(false);

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
            Fingerprint = fingerprint,
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

    /// <summary>
    /// The assembly's plugin class, or a <see cref="TurboPlugin"/> with nothing of its own when it
    /// has none: what it declares (commands, messages, routes...) is found by the scan either way.
    /// </summary>
    private static ITurboPlugin CreatePluginInstance(Assembly asm, PluginManifest manifest)
    {
        var pluginType = AssemblyExplorer.FindType(asm, typeof(ITurboPlugin));
        var plugin = pluginType is null
            ? new DefaultPlugin()
            : (ITurboPlugin)Activator.CreateInstance(pluginType)!;

        if (plugin is TurboPlugin conventional)
            conventional.UseManifest(manifest);

        return plugin;
    }

    /// <summary>The plugin of an assembly with no plugin class.</summary>
    private sealed class DefaultPlugin : TurboPlugin;

    /// <summary>
    /// The plugin's own container, the database contexts it registered, whose applied migrations
    /// the admin panel lists, and the config classes it binds, whose settings the panel shows. The
    /// host's configuration is in the container, so a plugin binds a class to its section as a
    /// module does (<c>services.AddOptions&lt;T&gt;().BindConfiguration(T.SECTION_NAME)</c>); the
    /// host binds its <see cref="PluginSettingsAttribute"/> classes and registers its
    /// <see cref="PluginDbContextBase{TContent}"/> contexts that it did not itself.
    /// </summary>
    private (
        ServiceProvider Services,
        List<Type> Contexts,
        List<Type> Settings,
        List<Type> Replaces
    ) CreatePluginServiceProvider(
        ITurboPlugin plugin,
        Assembly assembly,
        PluginManifest manifest,
        PluginExtensions extensions,
        PluginClientModules? clientModules,
        PluginContributionLog contributions
    )
    {
        var services = new ServiceCollection();

        services.AddSingleton(manifest);
        services.AddSingleton<IPluginContributionLog>(contributions);
        services.AddSingleton<IPluginCatalog>(new PluginCatalog(_exports));
        services.AddSingleton<IHostServices>(
            new HostServices(_hostForPlugins, extensions, clientModules)
        );
        services.ConfigurePrefixedLogging(host, manifest.Name);

        // The hotel API: the plugin's own services take it by constructor, as the classes its
        // assembly declares do from the host (docs/hotel-api.md).
        if (host.GetService<IHotel>() is { } hotel)
            services.AddSingleton(hotel);

        if (host.GetService<IConfiguration>() is { } configuration)
        {
            services.AddSingleton(configuration);
            services.AddWholeCollectionBinding();
        }

        var hostOnes = services.Count;

        plugin.ConfigureServices(services, manifest);

        var own = services
            .Skip(hostOnes)
            .Where(d => !d.IsKeyedService)
            .Select(d => d.ServiceType)
            .Distinct()
            .ToList();

        AddConventionalServices(services, assembly);
        BuildOwnTypesOverTheHost(services, assembly);

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

        return (
            services.BuildServiceProvider(SP_OPTIONS),
            contexts,
            settings,
            HostServicesReplaced(manifest, assembly, own)
        );
    }

    /// <summary>
    /// The host services among the ones the plugin registers that it replaces for the whole host
    /// (<see cref="IReplaceableServices"/>, <c>docs/plugin-services.md</c>). One the host has but
    /// does not let a plugin replace stays the plugin's own, which is logged: its classes get it,
    /// the host's never do.
    /// </summary>
    private List<Type> HostServicesReplaced(
        PluginManifest manifest,
        Assembly assembly,
        List<Type> own
    )
    {
        var replaceable = host.GetService<IReplaceableServices>();
        var known = host.GetService<IServiceProviderIsService>();
        var replaces = new List<Type>();

        foreach (var type in own)
        {
            if (replaceable?.IsReplaceable(type) == true)
            {
                replaces.Add(type);

                continue;
            }

            if (
                type.IsDefined(typeof(ReplaceableServiceAttribute), false)
                || (
                    type.Assembly != assembly
                    && type.Namespace?.StartsWith("Turbo.", StringComparison.Ordinal) == true
                    && known?.IsService(type) == true
                )
            )
                _logger.LogWarning(
                    "Plugin {Key} registers its own {Service}, which this host does not let a plugin replace: only the plugin's own classes get it",
                    manifest.Key,
                    type.Name
                );
        }

        return replaces;
    }

    /// <summary>
    /// The plugin's own classes it registers by type (<c>AddSingleton&lt;SkillBook&gt;()</c>, a
    /// hosted service) are built over its container and then the host's, as the classes its
    /// assembly declares are: they take <c>IGrainFactory</c>, a provider or a host service by
    /// constructor without a <c>Host&lt;T&gt;</c> factory. They reach the host as
    /// <see cref="IHostServices"/> does (the plugin's own packet registry and client modules), and
    /// the host is never asked for one of the plugin's types (<see cref="PluginHostServiceProvider"/>).
    /// Open generics and keyed services stay as registered.
    /// </summary>
    private void BuildOwnTypesOverTheHost(IServiceCollection services, Assembly assembly)
    {
        var hostForPlugins = _hostForPlugins;

        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];

            if (
                descriptor.IsKeyedService
                || descriptor.ImplementationType is not { } type
                || type.Assembly != assembly
                || type.IsGenericTypeDefinition
            )
                continue;

            services[i] = ServiceDescriptor.Describe(
                descriptor.ServiceType,
                sp =>
                    ActivatorUtilities.CreateInstance(
                        new CompositeServiceProvider(
                            sp,
                            sp.GetService<IHostServices>() as IServiceProvider ?? hostForPlugins
                        ),
                        type
                    ),
                descriptor.Lifetime
            );
        }
    }

    /// <summary>
    /// Binds the plugin's <see cref="PluginSettingsAttribute"/> classes to their sections and
    /// registers its <see cref="PluginDbContextBase{TContent}"/> contexts, each unless the plugin
    /// did already.
    /// </summary>
    private static void AddConventionalServices(IServiceCollection services, Assembly assembly)
    {
        var registered = services.Select(d => d.ServiceType).ToHashSet();

        foreach (var type in AssemblyExplorer.FindAssignees(assembly, typeof(object)))
        {
            if (
                type.GetCustomAttribute<PluginSettingsAttribute>() is { } settings
                && !registered.Contains(typeof(IOptionsChangeTokenSource<>).MakeGenericType(type))
            )
                BIND_SETTINGS.MakeGenericMethod(type).Invoke(null, [services, settings.Section]);

            if (IsPluginContext(type) && !registered.Contains(type))
                ADD_DATABASE.MakeGenericMethod(type).Invoke(null, [services]);
        }
    }

    private static readonly MethodInfo BIND_SETTINGS = typeof(PluginManager).GetMethod(
        nameof(BindSettings),
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    private static readonly MethodInfo ADD_DATABASE = typeof(DatabaseServices).GetMethod(
        nameof(DatabaseServices.AddPluginDatabase)
    )!;

    private static void BindSettings<T>(IServiceCollection services, string section)
        where T : class => services.AddOptions<T>().BindConfiguration(section);

    private static bool IsPluginContext(Type type)
    {
        for (var at = type.BaseType; at is not null; at = at.BaseType)
            if (at.IsGenericType && at.GetGenericTypeDefinition() == typeof(PluginDbContextBase<>))
                return true;

        return false;
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
        var policies = host.GetService<IGamePolicies>()?.Registered ?? [];

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
                    status.LoadedAtUtc,
                    [
                        .. policies.Where(x =>
                            string.Equals(x.Owner, m.Key, StringComparison.OrdinalIgnoreCase)
                        ),
                    ]
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
                    null,
                    []
                )
            );

        return [.. list];
    }

    /// <summary>The kinds whose names one live plugin alone may register: a second silently replaces the first.</summary>
    private static readonly ImmutableHashSet<string> UNIQUE_KINDS =
    [
        PluginContributionKinds.FURNI_LOGIC,
        PluginContributionKinds.COMMAND,
        PluginContributionKinds.SETTING,
        PluginContributionKinds.CAPABILITY,
        PluginContributionKinds.SERVICE,
    ];

    /// <summary>
    /// Everything the plugin contributes (<see cref="PluginContributions"/>): what its last load
    /// registered, the protocol it added while live, the names another live plugin registers too,
    /// and what clients report of its client half. Null when no plugin has the key.
    /// </summary>
    internal PluginContributions? ContributionsOf(string key)
    {
        if (!_status.TryGetValue(key, out var status))
            return null;

        var items = new List<PluginContribution>(
            _contributions.TryGetValue(key, out var log) ? log.Items : []
        );

        if (_live.TryGetValue(key, out var env))
        {
            items.AddRange(
                env.Extensions.Capabilities.Select(x => new PluginContribution(
                    PluginContributionKinds.CAPABILITY,
                    x,
                    null
                ))
            );
            items.AddRange(
                env.Extensions.IncomingHeaders.Select(x => new PluginContribution(
                    PluginContributionKinds.INCOMING_HEADER,
                    x.ToString(CultureInfo.InvariantCulture),
                    null
                ))
            );
            items.AddRange(
                env.Extensions.OutgoingHeaders.Select(x => new PluginContribution(
                    PluginContributionKinds.OUTGOING_HEADER,
                    x.ToString(CultureInfo.InvariantCulture),
                    null
                ))
            );
        }

        items.AddRange(
            status.Migrations.Select(x => new PluginContribution(
                PluginContributionKinds.MIGRATION,
                x,
                null
            ))
        );

        var conflicts = new List<string>();

        if (status.State == PluginStateType.Loaded)
            foreach (var (otherKey, other) in _contributions)
            {
                if (
                    string.Equals(otherKey, key, StringComparison.OrdinalIgnoreCase)
                    || !_live.ContainsKey(otherKey)
                )
                    continue;

                var theirs = other.Items;

                foreach (
                    var item in items.Where(x =>
                        UNIQUE_KINDS.Contains(x.Kind)
                        && theirs.Any(y => y.Kind == x.Kind && y.Name == x.Name)
                    )
                )
                    conflicts.Add($"{otherKey} registers the {item.Kind} {item.Name} too.");
            }

        var issues = host.GetService<IClientExtensionReports>()?.IssuesOf(key) ?? [];

        conflicts.AddRange(
            issues
                .Where(x => x.Kind == ClientExtensionReportKinds.PATCH_CONFLICT)
                .Select(x => $"{x.Subject}: {x.Message}")
        );

        return new PluginContributions(
            key,
            status.Manifest.Name,
            status.State,
            status.Error,
            [
                .. items
                    .Distinct()
                    .OrderBy(x => x.Kind, StringComparer.Ordinal)
                    .ThenBy(x => x.Name, StringComparer.Ordinal),
            ],
            [.. conflicts.Distinct()],
            issues
        );
    }

    /// <summary>
    /// Notes what the load built besides the assembly scan's registrations: its databases,
    /// settings, admin nodes, pages and endpoints, web routes and the template patches its
    /// <c>wwwroot/template-patches.json</c> declares.
    /// </summary>
    private void NoteContributions(
        PluginContributionLog log,
        string folder,
        List<Type> contexts,
        List<Type> settings,
        PluginAdminRoutes.Table? admin,
        ImmutableArray<PluginWebEndpoint> web
    )
    {
        foreach (var context in contexts)
            log.Add(PluginContributionKinds.DATABASE, context.Name);

        foreach (var type in settings)
            log.Add(
                PluginContributionKinds.SETTING,
                type.GetCustomAttribute<PluginSettingsAttribute>()?.Section ?? type.Name,
                type.Name
            );

        if (admin is not null)
        {
            foreach (var page in admin.Pages.Pages)
                log.Add(PluginContributionKinds.ADMIN_PAGE, page.Id, page.Title);

            foreach (var endpoint in admin.Endpoints)
                log.Add(
                    PluginContributionKinds.ADMIN_ENDPOINT,
                    $"{endpoint.Method} {endpoint.Pattern}",
                    endpoint.Node
                );

            foreach (var node in admin.Pages.Nodes)
                log.Add(PluginContributionKinds.PERMISSION_NODE, node);
        }

        foreach (var endpoint in web)
            log.Add(
                PluginContributionKinds.WEB_ROUTE,
                $"{endpoint.Method} {endpoint.Pattern}",
                endpoint.Anonymous ? "anonymous" : string.Join(", ", endpoint.Nodes)
            );

        foreach (var (template, ops) in PluginHelpers.ReadTemplatePatches(folder, _logger))
            log.Add(PluginContributionKinds.TEMPLATE_PATCH, template, $"{ops} op(s)");
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
        {
            Routes?.Off(m.Key);
            WebRoutes?.Off(m.Key);
        }

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
            WebRoutes?.Off(key);

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
        List<IHostedService> started,
        CancellationToken ct
    )
    {
        await ProcessMigrationsAsync(sp, ct).ConfigureAwait(false);

        foreach (var svc in sp.GetServices<IHostedService>())
        {
            try
            {
                await svc.StartAsync(ct).ConfigureAwait(false);
                started.Add(svc);
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

            foreach (var dbModule in sp.GetServices<IPluginDbModule>())
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
            await CancelStoppingAsync(env, ct).ConfigureAwait(false);

            foreach (var stopping in host.GetServices<IPluginStopping>())
            {
                try
                {
                    await stopping.StoppingAsync(env.Manifest.Key, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "{Type} failed as {Key} stopped",
                        stopping.GetType().Name,
                        env.Manifest.Key
                    );
                }
            }

            if (env.ServiceProvider is { } sp)
                await StopHostedServicesAsync(
                        sp.GetServices<IHostedService>(),
                        env.Manifest.Key,
                        ct
                    )
                    .ConfigureAwait(false);

            try
            {
                await env.Instance.StopAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop plugin {Key}", env.Manifest.Key);
            }

            await DisposeAllAsync(env.Disposables, env.Manifest.Key).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to destroy {Key}", env.Manifest.Key);
        }

        return env.Alc is null ? null : AssemblyMemoryLoader.BeginUnload(env.Alc);
    }

    /// <summary>How long an unload waits for a plugin's hotel-ready hook to end once it is cancelled.</summary>
    private static readonly TimeSpan READY_WAIT = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Calls the plugin's hotel-ready hook once the hotel has started: when it does at boot, at
    /// once on a reload or a later install (a token already cancelled runs its callback as it is
    /// registered). Without an application lifetime (a test's host) the hotel counts as up.
    /// </summary>
    private void WhenHotelReady(PluginEnvelope env)
    {
        if (env.Instance is not TurboPlugin plugin)
            return;

        if (host.GetService<IHostApplicationLifetime>() is not { } lifetime)
        {
            env.Ready = RunReadyAsync(env.Manifest.Key, plugin);

            return;
        }

        env.Disposables.Add(
            lifetime.ApplicationStarted.Register(() =>
                env.Ready = RunReadyAsync(env.Manifest.Key, plugin)
            )
        );
    }

    private Task RunReadyAsync(string key, TurboPlugin plugin) =>
        // Off the caller: the started callbacks run one after another on the host's start.
        Task.Run(async () =>
        {
            try
            {
                await plugin.ReadyAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The hotel-ready hook of plugin {Key} failed", key);
            }
        });

    /// <summary>
    /// The first step of an unload: the plugin's <see cref="IPluginContext.Stopping"/> is
    /// cancelled, and its hotel-ready hook, if still going, is waited for.
    /// </summary>
    private async Task CancelStoppingAsync(PluginEnvelope env, CancellationToken ct)
    {
        if (env.Instance is not TurboPlugin plugin)
            return;

        try
        {
            plugin.BeginStopping();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A stopping callback of plugin {Key} failed", env.Manifest.Key);
        }

        if (env.Ready is not { IsCompleted: false } ready)
            return;

        try
        {
            await ready.WaitAsync(READY_WAIT, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "The hotel-ready hook of plugin {Key} did not stop within {Wait}; the unload goes on without it",
                env.Manifest.Key,
                READY_WAIT
            );
        }
    }

    private async Task StopHostedServicesAsync(
        IEnumerable<IHostedService> services,
        string key,
        CancellationToken ct
    )
    {
        foreach (var svc in services)
        {
            try
            {
                await svc.StopAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hosted service stop failed for {Key}", key);
            }
        }
    }

    /// <summary>Disposes what a plugin registered, an async disposable as one.</summary>
    private async Task DisposeAllAsync(List<object> disposables, string key)
    {
        foreach (var inst in disposables)
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
                    key
                );
            }
        }

        disposables.Clear();
    }

    /// <summary>
    /// Waits for a plugin's context to be collected, and says so when something outside the
    /// plugin still holds it or a type loaded in it (a registration it did not dispose).
    /// </summary>
    private async Task WaitForUnloadAsync(WeakReference? alc, string key, CancellationToken ct)
    {
        if (alc is null)
            return;

        SerializerCaches.Clear();

        if (
            !await AssemblyMemoryLoader
                .WaitForUnloadAsync(alc, _config.UnloadWaitMs, ct, _config.UnloadMinCollections)
                .ConfigureAwait(false)
        )
            _logger.LogWarning(
                "ALC for plugin {Key} did not unload within timeout. Possible memory leak from retained type references.",
                key
            );
    }

    /// <summary>
    /// Waits for the context of a load that failed, off the load's path; <see cref="UnloadAllAsync"/>
    /// waits for it to be done.
    /// </summary>
    private void WaitForFailedUnload(WeakReference? alc, string key)
    {
        if (alc is null)
            return;

        var wait = Task.Run(() => WaitForUnloadAsync(alc, key, CancellationToken.None));

        _failedUnloads.TryAdd(wait, 0);
        _ = wait.ContinueWith(
            done => _failedUnloads.TryRemove(done, out _),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
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
