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
/// <c>appsettings.json</c> changed on disk - and <see cref="Version"/> goes up with it.
/// </summary>
internal sealed class ServerSettingsService : IServerSettings, IDisposable
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

    private int _version;
    private Read? _read;

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

        foreach (var section in _registry.Sections)
        {
            var defaults = Instance(section, () => Activator.CreateInstance(section.Type));
            var configured =
                Instance(section, () => _configuration.GetSection(section.Name).Get(section.Type))
                ?? defaults;
            var running = Instance(section, () => Running(section.Type));

            foreach (var setting in section.Settings)
            {
                // Two classes may share a section (Turbo:Navigator); the first names a setting.
                if (!registered.TryAdd(setting.Path, setting))
                    continue;

                var value = Json(setting, setting.ReadFrom(configured));
                var now = running is null ? value : Json(setting, setting.ReadFrom(running));
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
                        PendingRestart = value != now,
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

    private sealed record Read(
        int Version,
        ImmutableArray<ServerSettingSnapshot> Settings,
        Dictionary<string, ServerSettingSnapshot> ByPath,
        Dictionary<string, ServerSettingRegistry.Setting> Registered
    );
}
