using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Commands;
using Turbo.Contracts.Hotel;
using Turbo.Database.Configuration;
using Turbo.Database.Migrations;
using Turbo.Gamedata;
using Turbo.Gamedata.ClientModules;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Plugins;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Messages.PluginMessages;
using Turbo.Messages.Registry;
using Turbo.Networking.Extensions;
using Turbo.Players.Permissions;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Settings;
using Turbo.Primitives.Texts;
using Turbo.Rooms;
using Turbo.Rooms.Object.Logic;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Testing;

namespace Turbo.Tests.Support;

/// <summary>
/// A hotel's plugin host with the registries a plugin's pieces go to: modules, messages, texts,
/// settings, web routes, commands, permission nodes, furni logic, room listeners, and a migrator
/// with migrations off (asked for each context, touching no database).
/// </summary>
internal sealed class PluginTestHost
{
    private readonly Fakes _fakes;

    public PluginTestHost(SqliteDb db, Fakes fakes)
    {
        _fakes = fakes;

        var config = Options.Create(new GamedataConfig());
        var files = fakes.Create<IGamedataFileService>();
        var packets = new ExtensionPacketRegistry();

        // Migrations off: the migrator is asked for each context and touches no database.
        var database = Options.Create(
            new DatabaseConfig
            {
                ConnectionString = "Server=127.0.0.1;Port=1;Database=none;Connect Timeout=1",
                ServerVersion = "8.0.0-mysql",
                Migrate = MigrationMode.Off,
            }
        );

        Texts = new GamedataTextService(
            db,
            config,
            files,
            new GamedataWriteLock(),
            TimeProvider.System,
            fakes.Create<IHotelTextProvider>(),
            NullLogger<GamedataTextService>.Instance
        );
        Modules = new ClientModuleRegistry(
            new TestOptionsMonitor<GamedataConfig>(new GamedataConfig()),
            NullLogger<ClientModuleRegistry>.Instance
        );
        Messages = new PluginMessageDirectory(packets);
        Services = new ServiceCollection()
            .AddSingleton<IHotel>(Hotel)
            .AddSingleton<IClientModuleDirectory>(Modules)
            .AddSingleton<IExtensionPacketRegistry>(packets)
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddSingleton(fakes.Create<IPluginSettingSections>())
            .AddSingleton(WebRoutes)
            .AddSingleton(database)
            .AddSingleton(
                new DatabaseMigrator(database, fakes.Create<IMigrationLock>(), MigratorLog)
            )
            .AddSingleton<IPluginGamedataSeeds>(
                new PluginGamedataSeeds(
                    db,
                    fakes.Create<IGamedataVariableService>(),
                    Texts,
                    files,
                    Loaded,
                    Modules,
                    NullLogger<PluginGamedataSeeds>.Instance
                )
            )
            .BuildServiceProvider();
        Processors =
        [
            new PluginMessageFeatureProcessor(
                new MessageRegistry(Services, NullLogger<MessageRegistry>.Instance),
                Messages,
                NullLoggerFactory.Instance,
                Services
            ),
            (IAssemblyFeatureProcessor)
                Activator.CreateInstance(
                    typeof(SuggestionSourceRegistry).Assembly.GetType(
                        "Turbo.Commands.CommandFeatureProcessor",
                        throwOnError: true
                    )!,
                    fakes.Create<ICommandRegistryProvider>(),
                    new SuggestionSourceRegistry(),
                    new CommandArgumentParserRegistry()
                )!,
            new PermissionNodeFeatureProcessor(fakes.Create<IPermissionRegistryProvider>()),
            new RoomObjectLogicFeatureProcessor(fakes.Create<IRoomObjectLogicProvider>()),
            new RoomEventListenerFeatureProcessor(fakes.Create<IRoomEventListenerRegistry>()),
        ];
    }

    public ServiceProvider Services { get; }

    public GamedataTextService Texts { get; }

    /// <summary>The hotel API the host gives every plugin; in memory here.</summary>
    public FakeHotel Hotel { get; } = new();

    public ClientModuleRegistry Modules { get; }

    public PluginMessageDirectory Messages { get; }

    public LoadedPluginGamedata Loaded { get; } = new();

    public PluginWebRoutes WebRoutes { get; } = new();

    public CapturingLogger<DatabaseMigrator> MigratorLog { get; } = new();

    /// <summary>The settings class the plugin's settings were listed with.</summary>
    public Type? SettingsType =>
        _fakes
            .Log.Of(nameof(IPluginSettingSections.Add))
            .Select(c => ((IReadOnlyList<Type>)c.Args[1]!).Single())
            .LastOrDefault();

    private IAssemblyFeatureProcessor[] Processors { get; }

    public PluginManager Manager(string folder) =>
        new(
            Services,
            new AssemblyProcessor(Processors),
            Options.Create(new PluginConfig { PluginFolderPath = folder }),
            NullLogger<PluginManager>.Instance
        );
}
