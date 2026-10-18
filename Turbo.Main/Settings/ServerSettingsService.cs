using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Turbo.Database.Context;
using Turbo.Database.Entities.Settings;
using Turbo.Primitives.Players;
using Turbo.Primitives.Settings;
using Turbo.Primitives.Settings.Enums;
using Turbo.Primitives.Settings.Snapshots;

namespace Turbo.Main.Settings;

/// <summary>
/// The server's settings (<see cref="IServerSettings"/>): read from the registered config
/// classes (<see cref="ServerSettingRegistry"/>) and the configuration, and overridden in
/// <c>server_settings</c>, which <see cref="ServerSettingsConfigurationProvider"/> reads back.
/// What is shown is worked out again whenever the configuration reloads - an override saved, or
/// <c>appsettings.json</c> changed on disk, a plugin's sections added or taken away - and
/// <see cref="Version"/> goes up with it.
/// </summary>
internal sealed class ServerSettingsService : IServerSettings, IPluginSettingSections, IDisposable
{
    private const string ENVIRONMENT_SEPARATOR = "__";
    private const string COMMAND_LINE = "command line";
    private const string ANY_VARIABLE = "an environment variable";

    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _services;
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly ServerSettingRegistry _registry;
    private readonly ServerSettingsConfig _config;
    private readonly ILogger<ServerSettingsService> _logger;
    private readonly SemaphoreSlim _writes = new(1, 1);
    private readonly IDisposable _reloads;

    private readonly Lock _plugins = new();

    private int _version;
    private Read? _read;
    private ImmutableArray<PluginSection> _pluginSections = [];

    public ServerSettingsService(
        IConfiguration configuration,
        IServiceProvider services,
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        ServerSettingRegistry registry,
        IOptions<ServerSettingsConfig> config,
        ILogger<ServerSettingsService> logger
    )
    {
        _configuration = configuration;
        _services = services;
        _dbCtxFactory = dbCtxFactory;
        _registry = registry;
        _config = config.Value;
        _logger = logger;

        // Read as the server started, before there was a logger: the server runs without its
        // panel overrides, so say why.
        if (ServerSettingsConfiguration.Find(configuration)?.LoadError is { } error)
            _logger.LogWarning("Reading the panel's server settings: {Error}", error);

        _reloads = ChangeToken.OnChange(
            configuration.GetReloadToken,
            () => Interlocked.Increment(ref _version)
        );
    }

    public int Version => Volatile.Read(ref _version);

    public ImmutableArray<ServerSettingSnapshot> List() => Current().Settings;

    public ServerSettingSnapshot? Get(string path) =>
        Current().ByPath.GetValueOrDefault(path.Trim());

    public string? GetValue(string path) =>
        Get(path) is { Secret: false } setting ? setting.Value : null;

    public async Task<ServerSettingSnapshot> SaveAsync(
        string path,
        string value,
        PlayerId player,
        CancellationToken ct
    )
    {
        var (setting, snapshot) = Writable(path);
        var json = SettingJson.Normalize(value, setting.Type);
        // What is kept: for a list or map, only what its defaults don't already say.
        var stored = SettingJson.BeyondDefaults(json, snapshot.Default, setting.Kind);

        await _writes.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbCtxScope = dbCtx.ConfigureAwait(false);

            var row = await dbCtx
                .ServerSettings.FirstOrDefaultAsync(x => x.Path == setting.Path, ct)
                .ConfigureAwait(false);
            if (row?.Value == stored)
                return snapshot;

            // The history keeps the values as staff saw them: a list whole, defaults and all.
            var before = row is null ? null : snapshot.Value;

            if (row is null)
                dbCtx.ServerSettings.Add(
                    new ServerSettingEntity { Path = setting.Path, Value = stored }
                );
            else
                row.Value = stored;

            dbCtx.ServerSettingChanges.Add(Change(setting, before, json, player));

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Player {PlayerId} set the server setting {Path}",
                player.Value,
                setting.Path
            );
        }
        finally
        {
            _writes.Release();
        }

        Refresh();

        return Get(setting.Path)!;
    }

    public async Task<bool> ResetAsync(string path, PlayerId player, CancellationToken ct)
    {
        var (setting, snapshot) = Writable(path);

        await _writes.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbCtxScope = dbCtx.ConfigureAwait(false);

            var row = await dbCtx
                .ServerSettings.FirstOrDefaultAsync(x => x.Path == setting.Path, ct)
                .ConfigureAwait(false);

            if (row is null)
                return false;

            dbCtx.ServerSettings.Remove(row);
            dbCtx.ServerSettingChanges.Add(Change(setting, snapshot.Value, null, player));

            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Player {PlayerId} put the server setting {Path} back to the files' value",
                player.Value,
                setting.Path
            );
        }
        finally
        {
            _writes.Release();
        }

        Refresh();

        return true;
    }

    public async Task<ServerSettingHistoryPage> HistoryAsync(int page, CancellationToken ct)
    {
        var size = Math.Max(1, _config.HistoryPageSize);
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var total = await dbCtx.ServerSettingChanges.CountAsync(ct).ConfigureAwait(false);
        var rows = await dbCtx
            .ServerSettingChanges.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Skip(Math.Max(0, page) * size)
            .Take(size)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new ServerSettingHistoryPage
        {
            Items =
            [
                .. rows.Select(x => new ServerSettingChangeSnapshot
                {
                    Id = x.Id,
                    Path = x.Path,
                    Before = x.Before,
                    After = x.After,
                    Secret = x.Secret,
                    PlayerId = x.PlayerEntityId,
                    ChangedAt = x.CreatedAt,
                }),
            ],
            Total = total,
            PageSize = size,
        };
    }

    public IDisposable Add(
        string plugin,
        IReadOnlyList<Type> types,
        Func<Type, object?> running,
        string? docsFile
    )
    {
        var summary = SettingDocs.From(docsFile);
        var added = types
            .Select(type => ServerSettingRegistry.PluginSection(type, plugin, summary))
            .OfType<ServerSettingRegistry.Section>()
            .Select(section => new PluginSection(section, running))
            .ToList();

        lock (_plugins)
        {
            var taken = _registry
                .Sections.Select(x =>
                    (x.Name, Paths: x.Settings.Select(y => y.Path), Owner: "the server")
                )
                .Concat(
                    _pluginSections.Select(x =>
                        (
                            x.Section.Name,
                            Paths: x.Section.Settings.Select(y => y.Path),
                            Owner: $"the plugin {x.Section.Plugin}"
                        )
                    )
                )
                .ToList();

            foreach (var section in added.Select(x => x.Section))
            {
                var owner = taken
                    .Where(x =>
                        string.Equals(x.Name, section.Name, StringComparison.OrdinalIgnoreCase)
                        || x.Paths.Intersect(
                                section.Settings.Select(y => y.Path),
                                StringComparer.OrdinalIgnoreCase
                            )
                            .Any()
                    )
                    .Select(x => x.Owner)
                    .FirstOrDefault();

                if (owner is not null)
                    throw new ArgumentException(
                        $"The plugin {plugin}'s settings {section.Name} are {owner}'s already.",
                        nameof(types)
                    );
            }

            _pluginSections = _pluginSections.AddRange(added);
        }

        Changed();

        return new Removal(this, added);
    }

    public void Dispose()
    {
        _reloads.Dispose();
        _writes.Dispose();
    }

    /// <summary>
    /// The setting at the path, if the panel may change it: known, not one the panel stands on,
    /// not one the environment sets. Throws <see cref="ArgumentException"/> otherwise.
    /// </summary>
    private (ServerSettingRegistry.Setting Setting, ServerSettingSnapshot Snapshot) Writable(
        string path
    )
    {
        var read = Current();
        path = path.Trim();

        if (
            !read.ByPath.TryGetValue(path, out var snapshot)
            || !read.Registered.TryGetValue(path, out var setting)
        )
            throw new ArgumentException($"There is no setting {path}.", nameof(path));

        if (setting.Startup)
            throw new ArgumentException(
                $"{setting.Path} is one the panel stands on: change it in appsettings.json or the environment.",
                nameof(path)
            );

        if (snapshot.Source == ServerSettingSource.Environment)
            throw new ArgumentException(
                $"{setting.Path} is set by {snapshot.SourceName}, which the panel can't override.",
                nameof(path)
            );

        return (setting, snapshot);
    }

    private static ServerSettingChangeEntity Change(
        ServerSettingRegistry.Setting setting,
        string? before,
        string? after,
        PlayerId player
    ) =>
        new()
        {
            Path = setting.Path,
            Before = setting.Secret ? null : before,
            After = setting.Secret ? null : after,
            Secret = setting.Secret,
            PlayerEntityId = player.Value,
        };

    /// <summary>Reads the overrides again; the configuration's reload moves <see cref="Version"/>.</summary>
    private void Refresh()
    {
        if (ServerSettingsConfiguration.Find(_configuration) is { } provider)
        {
            provider.Refresh();

            if (provider.LoadError is { } error)
                _logger.LogWarning("Reading the panel's server settings: {Error}", error);
        }
        else
        {
            // Without the provider (a host built without it) the override can't apply; say so.
            _logger.LogWarning(
                "The panel's server settings aren't in the configuration, so overrides don't apply"
            );
            Interlocked.Increment(ref _version);
        }
    }

    private void Remove(IReadOnlyList<PluginSection> sections)
    {
        lock (_plugins)
            _pluginSections = _pluginSections.RemoveRange(sections);

        Changed();
    }

    /// <summary>
    /// What is shown is built again; the last read is let go at once, as it holds the plugin's
    /// types, which would keep a plugin taken away from unloading.
    /// </summary>
    private void Changed()
    {
        _read = null;
        Interlocked.Increment(ref _version);
    }

    private Read Current()
    {
        var version = Version;

        if (_read is { } read && read.Version == version)
            return read;

        read = Build(version);
        _read = read;

        return read;
    }

    private Read Build(int version)
    {
        var settings = new List<ServerSettingSnapshot>();
        var registered = new Dictionary<string, ServerSettingRegistry.Setting>(
            StringComparer.OrdinalIgnoreCase
        );
        var panel = ServerSettingsConfiguration.Find(_configuration);
        var providers = (_configuration as IConfigurationRoot)?.Providers.Reverse().ToList() ?? [];

        var plugins = _pluginSections;

        foreach (
            var (section, plugin) in _registry
                .Sections.Select(x => (x, (PluginSection?)null))
                .Concat(plugins.Select(x => (x.Section, (PluginSection?)x)))
        )
        {
            var defaults = Instance(section, () => Activator.CreateInstance(section.Type));
            var configured =
                Instance(section, () => _configuration.GetSection(section.Name).Get(section.Type))
                ?? defaults;
            // A plugin's own value is what it reads through its monitor: running and live alike.
            var running = Instance(
                section,
                () => plugin is null ? Running(section.Type) : plugin.Running(section.Type)
            );
            var live =
                plugin is not null ? running
                : section.Settings.Any(x => x.Live) ? Instance(section, () => Live(section.Type))
                : null;
            // What a restart would run with: the configuration as it is now, built the way the
            // server builds its options, so what a module fills in (an empty list given its
            // defaults) is no change waiting on one.
            var next = plugin is null
                ? Instance(section, () => Fresh(section.Type)) ?? configured
                : configured;

            foreach (var setting in section.Settings)
            {
                // Two classes may share a section (Turbo:Navigator); the first names a setting.
                if (!registered.TryAdd(setting.Path, setting))
                    continue;

                var value = Json(setting, setting.ReadFrom(configured));
                // A live setting runs with what its readers read now; the others with what the
                // server took as it started.
                var reads = setting.Live ? live : running;
                var now = reads is null ? value : Json(setting, setting.ReadFrom(reads));
                var restarted = reads is null ? value : Json(setting, setting.ReadFrom(next));
                var (source, name) = SourceOf(setting, providers, panel);

                settings.Add(
                    new ServerSettingSnapshot
                    {
                        Path = setting.Path,
                        Section = section.Name,
                        Kind = setting.Kind,
                        Options = setting.Options,
                        Summary = setting.Summary,
                        Secret = setting.Secret,
                        Startup = setting.Startup,
                        Live = setting.Live,
                        Default = setting.Secret ? null : Json(setting, setting.ReadFrom(defaults)),
                        Value = setting.Secret ? null : value,
                        Running = setting.Secret ? null : now,
                        IsSet =
                            value
                                is not null
                                    and not "null"
                                    and not "\"\""
                                    and not "[]"
                                    and not "{}",
                        Source = source,
                        SourceName = name,
                        PendingRestart = restarted != now,
                        Plugin = section.Plugin,
                    }
                );
            }
        }

        settings.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path));

        return new Read(
            version,
            [.. settings],
            settings.ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase),
            registered
        );
    }

    /// <summary>The options the server took as it started, when something has asked for them.</summary>
    private object? Running(Type type)
    {
        var options = _services.GetService(typeof(IOptions<>).MakeGenericType(type));

        return options?.GetType().GetProperty(nameof(IOptions<object>.Value))?.GetValue(options);
    }

    /// <summary>
    /// The options built afresh from the configuration as it is now, through every step the
    /// server's own go through (binding, then what modules configure after it); null when they
    /// don't validate, as a restart would refuse them.
    /// </summary>
    private object? Fresh(Type type)
    {
        var factory = _services.GetService(typeof(IOptionsFactory<>).MakeGenericType(type));

        try
        {
            return factory
                ?.GetType()
                .GetMethod(nameof(IOptionsFactory<object>.Create))
                ?.Invoke(factory, [Options.DefaultName]);
        }
        catch (System.Reflection.TargetInvocationException ex)
            when (ex.InnerException is OptionsValidationException)
        {
            return null;
        }
    }

    /// <summary>The options as a reader that reads them as it uses them gets them now.</summary>
    private object? Live(Type type)
    {
        var monitor = _services.GetService(typeof(IOptionsMonitor<>).MakeGenericType(type));

        return monitor
            ?.GetType()
            .GetProperty(nameof(IOptionsMonitor<object>.CurrentValue))
            ?.GetValue(monitor);
    }

    private object? Instance(ServerSettingRegistry.Section section, Func<object?> make)
    {
        try
        {
            return make();
        }
        catch (Exception ex)
            when (ex
                    is InvalidOperationException
                        or MissingMethodException
                        or System.Reflection.TargetInvocationException
            )
        {
            // A value the binder can't read (text where a number goes): the page shows nothing
            // for the section rather than failing whole.
            _logger.LogWarning(ex, "The settings of {Section} couldn't be read", section.Name);

            return null;
        }
    }

    private static string? Json(ServerSettingRegistry.Setting setting, object? value) =>
        SettingJson.Write(value, setting.Type);

    /// <summary>The provider read last that gives the setting a value: what sets it.</summary>
    private (ServerSettingSource Source, string? Name) SourceOf(
        ServerSettingRegistry.Setting setting,
        IReadOnlyList<IConfigurationProvider> providers,
        ServerSettingsConfigurationProvider? panel
    )
    {
        var whole = setting.Kind is "list" or "map" or "json";

        foreach (var provider in providers)
        {
            var sets = ReferenceEquals(provider, panel)
                ? panel!.Overrides(setting.Path)
                : (provider.TryGet(setting.Path, out var value) && (value is not null || !whole))
                    || (whole && provider.GetChildKeys([], setting.Path).Any());

            if (!sets)
                continue;

            return ServerSettingsConfiguration.Unshadowed(provider) switch
            {
                ServerSettingsConfigurationProvider => (ServerSettingSource.Panel, null),
                EnvironmentVariablesConfigurationProvider => (
                    ServerSettingSource.Environment,
                    VariableOf(setting.Path)
                ),
                CommandLineConfigurationProvider => (ServerSettingSource.Environment, COMMAND_LINE),
                JsonConfigurationProvider json => (
                    ServerSettingSource.AppSettings,
                    Path.GetFileName(json.Source.Path)
                ),
                var other => (ServerSettingSource.AppSettings, other.GetType().Name),
            };
        }

        return (ServerSettingSource.Default, null);
    }

    /// <summary>The environment variable that sets the path, among those the configuration reads.</summary>
    private string VariableOf(string path)
    {
        var name = path.Replace(
            SettingJson.KEY_DELIMITER.ToString(),
            ENVIRONMENT_SEPARATOR,
            StringComparison.Ordinal
        );
        var prefixes =
            (_configuration as IConfigurationBuilder)
                ?.Sources.OfType<EnvironmentVariablesConfigurationSource>()
                .Select(x => x.Prefix ?? string.Empty)
                .Distinct()
            ?? [string.Empty];

        foreach (var prefix in prefixes)
        foreach (var candidate in new[] { prefix + name, prefix + path })
            if (Environment.GetEnvironmentVariable(candidate) is not null)
                return candidate;

        return ANY_VARIABLE;
    }

    private sealed record PluginSection(
        ServerSettingRegistry.Section Section,
        Func<Type, object?> Running
    );

    private sealed class Removal(ServerSettingsService owner, IReadOnlyList<PluginSection> sections)
        : IDisposable
    {
        private int _done;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0)
                owner.Remove(sections);
        }
    }

    private sealed record Read(
        int Version,
        ImmutableArray<ServerSettingSnapshot> Settings,
        Dictionary<string, ServerSettingSnapshot> ByPath,
        Dictionary<string, ServerSettingRegistry.Setting> Registered
    );
}
