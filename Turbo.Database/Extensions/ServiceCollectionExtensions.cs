using System;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Turbo.Contracts.Plugins;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Delegates;
using Turbo.Database.Migrations;
using Turbo.Database.Movements;
using Turbo.Database.Tracking;
using Turbo.Primitives.Tracking;

namespace Turbo.Database.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The server version per connection string, asked of the server once. A plugin context's
    /// options are built per scope, and <see cref="ServerVersion.AutoDetect(string)"/> opens a
    /// connection and queries the version each time it is called.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Lazy<ServerVersion>> SERVER_VERSIONS = new(
        StringComparer.Ordinal
    );

    /// <summary>
    /// The configured server version when there is one, which needs no connection; otherwise
    /// the server's own, asked on a connection without the database name: the version does not
    /// depend on it, and the database may be one the server has yet to create.
    /// </summary>
    private static ServerVersion GetServerVersion(DatabaseConfig config) =>
        !string.IsNullOrWhiteSpace(config.ServerVersion)
            ? ServerVersion.Parse(config.ServerVersion)
            : SERVER_VERSIONS
                .GetOrAdd(
                    config.ConnectionString,
                    static key => new Lazy<ServerVersion>(() =>
                        ServerVersion.AutoDetect(
                            new MySqlConnectionStringBuilder(key)
                            {
                                Database = string.Empty,
                            }.ConnectionString
                        )
                    )
                )
                .Value;

    public static IServiceCollection AddTurboDatabaseContext(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
    {
        services.Configure<DatabaseConfig>(
            builder.Configuration.GetSection(DatabaseConfig.SECTION_NAME)
        );

        // One migrator for the emulator's tables and every plugin's, and one lock between them.
        services.AddSingleton<IMigrationLock, MySqlMigrationLock>();
        services.AddSingleton<DatabaseMigrator>();

        // Pooled: every grain turn that touches the database creates a context, and TurboDbContext
        // holds nothing but its options, so a reset context from the pool serves as a new one.
        // Consumers still take IDbContextFactory<TurboDbContext>.
        services.AddPooledDbContextFactory<TurboDbContext>(
            (sp, options) =>
                UseTurboMySql(
                    options,
                    sp.GetRequiredService<IOptions<DatabaseConfig>>().Value,
                    mysql => mysql.MigrationsAssembly("Turbo.Database")
                )
        );

        return services;
    }

    /// <summary>
    /// The asset movement recorder (<c>Turbo:Movements</c>, <c>docs/movements.md</c>); also the
    /// hosted service that writes its marker rows.
    /// </summary>
    public static IServiceCollection AddTurboMovements(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
    {
        services
            .AddOptions<MovementsConfig>()
            .Bind(builder.Configuration.GetSection(MovementsConfig.SECTION_NAME))
            .Validate(
                x =>
                    x.PollMs > 0
                    && x.BatchSize > 0
                    && x.OutboxRetentionHours >= 0
                    && x.SettleMs >= 0
                    && x.ReReadWindowMs >= 0,
                "Turbo:Movements limits must be positive."
            )
            .ValidateOnStart();

        services.AddSingleton<AssetMovementRecorder>();
        services.AddSingleton<IAssetMovementRecorder>(sp =>
            sp.GetRequiredService<AssetMovementRecorder>()
        );
        services.AddHostedService(sp => sp.GetRequiredService<AssetMovementRecorder>());

        return services;
    }

    /// <summary>
    /// The client telemetry recorder and its maintenance (<c>Turbo:ClientTelemetry</c>): what
    /// clients report about themselves, kept for the admin panel's Client health and Usage pages.
    /// </summary>
    public static IServiceCollection AddTurboClientTelemetry(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
    {
        services
            .AddOptions<ClientTelemetryConfig>()
            .Bind(builder.Configuration.GetSection(ClientTelemetryConfig.SECTION_NAME))
            .Validate(
                x =>
                    x.RawRetentionDays >= 0
                    && x.RollupRetentionDays >= 0
                    && x.QueueCapacity > 0
                    && x.BatchSize > 0
                    && x.FlushIntervalMs > 0
                    && x.MaintenanceIntervalMinutes > 0
                    && x.LoginTrackingMinutes > 0,
                "Turbo:ClientTelemetry limits must be positive."
            )
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ClientTelemetryRecorder>();
        services.AddSingleton<IClientTelemetry>(sp =>
            sp.GetRequiredService<ClientTelemetryRecorder>()
        );
        services.AddHostedService(sp => sp.GetRequiredService<ClientTelemetryRecorder>());
        services.AddSingleton<ClientTelemetryMaintenance>();
        services.AddHostedService(sp => sp.GetRequiredService<ClientTelemetryMaintenance>());

        return services;
    }

    public static IServiceCollection AddPluginTablePrefix<TContext>(
        this IServiceCollection services
    )
        where TContext : DbContext
    {
        services.AddSingleton<TablePrefixProvider>(sp =>
        {
            var manifest = sp.GetRequiredService<PluginManifest>();

            // Used exactly as the manifest writes it, separator included ("tsp_"): the plugin's
            // design-time context factory builds its migrations from the same value, so deriving
            // or suffixing one here would rename tables the migrations already created.
            return () => manifest.TablePrefix ?? string.Empty;
        });

        return services;
    }

    public static IServiceCollection AddPluginDatabaseContext<TContext, TModule>(
        this IServiceCollection services
    )
        where TContext : DbContext
        where TModule : class, IPluginDbModule
    {
        AddPluginDatabaseServices<TContext, TModule>(services);

        services.AddDbContext<TContext>(ConfigurePluginContext);

        return services;
    }

    /// <summary>
    /// A plugin's <see cref="PluginDbContextBase{TContent}"/> with its tables migrated as the
    /// plugin loads and dropped by an uninstall that drops its data. The host calls it for every
    /// such context in the plugin's assembly that the plugin did not register itself, so a plugin
    /// rarely calls it. A context with a constructor taking its options is registered as
    /// <see cref="AddPluginDatabaseContext{TContext, TModule}"/> does; one with none is given its
    /// options and prefix as it is made.
    /// </summary>
    public static IServiceCollection AddPluginDatabase<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        if (
            typeof(TContext).GetConstructor([
                typeof(DbContextOptions<TContext>),
                typeof(TablePrefixProvider),
            ])
                is not null
            || typeof(TContext).GetConstructor([typeof(DbContextOptions<TContext>)]) is not null
        )
            return services.AddPluginDatabaseContext<TContext, PluginDbModule<TContext>>();

        if (!typeof(PluginDbContextBase<TContext>).IsAssignableFrom(typeof(TContext)))
            throw new ArgumentException(
                $"{typeof(TContext).Name} takes no options: it must derive from PluginDbContextBase<{typeof(TContext).Name}>.",
                nameof(TContext)
            );

        AddPluginDatabaseServices<TContext, PluginDbModule<TContext>>(services);

        services.AddScoped(sp =>
        {
            var context = (TContext)Activator.CreateInstance(typeof(TContext), nonPublic: true)!;

            ((PluginDbContextBase<TContext>)(object)context).UseHost(
                sp.GetRequiredService<TablePrefixProvider>(),
                options => ConfigurePluginContext(sp, options)
            );

            return context;
        });

        return services;
    }

    private static void AddPluginDatabaseServices<TContext, TModule>(IServiceCollection services)
        where TContext : DbContext
        where TModule : class, IPluginDbModule
    {
        services.AddPluginTablePrefix<TContext>();
        services.AddTransient<IPluginDbModule, TModule>();

        // EF Core keeps each context's model in an internal service provider that it caches for
        // the whole process; one holding a plugin's context would keep the plugin's assembly
        // load context alive after it unloads. The plugin's own goes with the plugin's services.
        services.TryAddSingleton(sp => new PluginEntityFrameworkServices(
            sp.GetRequiredService<IHostServices>()
                .GetRequiredService<IOptions<DatabaseConfig>>()
                .Value
        ));
    }

    private static void ConfigurePluginContext(IServiceProvider sp, DbContextOptionsBuilder options)
    {
        var prefix = sp.GetRequiredService<TablePrefixProvider>();
        var host = sp.GetRequiredService<IHostServices>();

        options.UseInternalServiceProvider(
            sp.GetRequiredService<PluginEntityFrameworkServices>().Services
        );

        // Its logging is the internal services' to set (UseLoggerFactory is refused with them).
        UseTurboMySql(
            options,
            host.GetRequiredService<IOptions<DatabaseConfig>>().Value,
            mysql => mysql.MigrationsHistoryTable(MigrationHelper.HistoryTableOf(prefix())),
            ownLogging: false
        );
    }

    /// <summary>
    /// The internal services EF Core gives a plugin's contexts (their models among them), built
    /// for the plugin and disposed with its services as it unloads, rather than EF's
    /// process-wide cache.
    /// </summary>
    internal sealed class PluginEntityFrameworkServices(DatabaseConfig dbConfig) : IDisposable
    {
        public ServiceProvider Services { get; } = Build(dbConfig);

        public void Dispose() => Services.Dispose();

        private static ServiceProvider Build(DatabaseConfig dbConfig)
        {
            var services = new ServiceCollection();

            // As UseTurboMySql: with LoggingEnabled off, EF is given no logger at all.
            if (!dbConfig.LoggingEnabled)
                services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

            return services.AddEntityFrameworkMySql().BuildServiceProvider();
        }
    }

    // The emulator's and every plugin's contexts connect the same way; only the migrations
    // setup differs. With LoggingEnabled off, EF is given no logger at all, so its command and
    // change-tracking logs stay out of the host log whatever the log level filters say.
    private static void UseTurboMySql(
        DbContextOptionsBuilder options,
        DatabaseConfig dbConfig,
        Action<MySqlDbContextOptionsBuilder> configureMySql,
        bool ownLogging = true
    )
    {
        var connectionString = dbConfig.ConnectionString;

        options.UseMySql(connectionString, GetServerVersion(dbConfig), configureMySql);

        if (ownLogging && !dbConfig.LoggingEnabled)
            options.UseLoggerFactory(NullLoggerFactory.Instance);
    }
}
