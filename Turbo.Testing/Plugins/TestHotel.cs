using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Commands;
using Turbo.Contracts.Approvals;
using Turbo.Contracts.Hotel;
using Turbo.Contracts.Plugins;
using Turbo.Database.Context;
using Turbo.Events;
using Turbo.Events.Registry;
using Turbo.Messages;
using Turbo.Messages.PluginMessages;
using Turbo.Messages.Registry;
using Turbo.Networking.Extensions;
using Turbo.Operations.Scheduling;
using Turbo.Pipeline;
using Turbo.Players.Permissions;
using Turbo.Players.Policies;
using Turbo.Plugins;
using Turbo.Plugins.Approvals;
using Turbo.Plugins.Compilation;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Events;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Texts;
using Turbo.Rooms;
using Turbo.Rooms.Object.Logic;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Testing;

/// <summary>
/// A hotel for a plugin's tests (docs/plugin-testing.md): <see cref="LoadAsync{TPlugin}"/> loads the
/// plugin as a server does (its sources compiled and held to the policy, its commands, messages,
/// handlers, policies, furni logic, scheduled tasks, web and admin routes, settings and tables
/// registered, its services started), and the test then plays the game against it: players who
/// say commands and send messages, purchases asked of its policies, requests approved by staff,
/// furni walked on and used, a clock that moves when told. The hotel API it gets is
/// <see cref="FakeHotel"/>, its tables live in an in-memory SQLite database, and a host service the
/// kit does not play is a recording stand-in (<see cref="Host"/>). One test at a time; dispose it at
/// the end.
/// </summary>
public sealed class TestHotel : IAsyncDisposable
{
    /// <summary>
    /// The run's compiles: the first hotel to load a plugin's sources compiles them, every later
    /// one (and any loading them at the same time) takes that compile and loads its own copy.
    /// </summary>
    private static readonly PluginCompileMemory COMPILES = new();

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-test-hotel-" + Guid.NewGuid().ToString("N")
    );
    private readonly SqliteConnection _db = new("Data Source=:memory:");
    private readonly MemoryConfigurationProvider _settings;
    private readonly IConfigurationRoot _configuration;
    private readonly ServiceProvider _host;
    private readonly PluginManager _manager;
    private readonly ConcurrentDictionary<string, Assembly> _builds = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Dictionary<string, TestPlayer> _players = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Dictionary<int, TestRoom> _rooms = [];
    private readonly List<Action<PluginManifest, IServiceCollection>> _replacements = [];
    private readonly TestLifetime _lifetime = new();
    private readonly TestServerDatabase _serverDb = new();

    private TestHotel(DateTimeOffset? now)
    {
        Fake = new FakeHotel(now);
        _db.Open();
        _configuration = new ConfigurationBuilder().Add(new MemoryConfigurationSource()).Build();
        _settings = (MemoryConfigurationProvider)_configuration.Providers.Single();
        Settings = new TestSettings(this, _configuration);
        Logs = new TestLogs();
        Sessions = new StandIns();
        Purchases = new TestPurchases(this);
        Schedule = new TestSchedule(this);
        Web = new TestWeb(this);
        Admin = new TestAdmin(this);
        Events = new TestEvents(this);
        Texts = new TestTexts(this);
        Ai = new AiFake(Fake);
        Approvals = new TestApprovals(this);

        AnswerHostCalls();

        var packets = new ExtensionPacketRegistry();
        var services = new ServiceCollection()
            .AddLogging(b => b.AddProvider(Logs).SetMinimumLevel(LogLevel.Debug))
            .AddSingleton<IHotel>(Fake)
            .AddSingleton<IPluginAiHost>(Ai)
            .AddSingleton<TimeProvider>(Fake.Clock)
            .AddSingleton<IConfiguration>(_configuration)
            .AddSingleton<IHostApplicationLifetime>(_lifetime)
            .AddSingleton<IExtensionPacketRegistry>(packets)
            .AddSingleton<IPluginMessageDirectory>(sp => new PluginMessageDirectory(
                sp.GetRequiredService<IExtensionPacketRegistry>()
            ))
            .AddSingleton(new PluginWebRoutes())
            .AddSingleton(new PluginAdminRoutes())
            .AddSingleton<IGrainFactory>(_ => Host.Of<IGrainFactory>())
            .AddSingleton<ISessionGateway>(_ => Host.Of<ISessionGateway>())
            .AddSingleton<IPermissionRegistryProvider>(_ => Host.Of<IPermissionRegistryProvider>())
            .AddSingleton<IRoomObjectLogicProvider>(_ => Host.Of<IRoomObjectLogicProvider>())
            .AddSingleton<IRoomEventListenerRegistry>(_ => Host.Of<IRoomEventListenerRegistry>())
            .AddSingleton(sp => new GamePolicies(sp.GetRequiredService<ILogger<GamePolicies>>()))
            .AddSingleton<IGamePolicies>(sp => sp.GetRequiredService<GamePolicies>())
            .AddSingleton<ICommandRegistryProvider>(sp => new CommandRegistryProvider(
                sp.GetRequiredService<ILogger<ICommandRegistryProvider>>(),
                sp.GetRequiredService<CommandArgumentParserRegistry>()
            ))
            .AddSingleton<CommandArgumentParserRegistry>()
            .AddSingleton<SuggestionSourceRegistry>()
            .AddSingleton<MessageRegistry>()
            .AddSingleton<EnvelopeInvokerFactory<MessageContext>>()
            .AddSingleton<EventRegistry>()
            .AddSingleton<EnvelopeInvokerFactory<EventContext>>()
            .AddSingleton<IEventPublisher>(_ => Events)
            .AddSingleton(sp => new EventSystem(sp.GetRequiredService<EventRegistry>()))
            .AddSingleton<IHotelTextProvider>(Texts)
            .AddSingleton<IPlayerNoticeService>(new TestNotices(Fake, Texts))
            .AddSingleton<ICommandLogWriter>(CommandLog)
            .AddSingleton<ICommandBatchExecutor, CommandBatchExecutor>()
            .AddSingleton<IOperatorCommandRunner, OperatorCommandRunner>()
            .AddSingleton(sp => new ScheduledTaskHost(
                sp.GetRequiredService<IGrainFactory>(),
                Fake.Clock,
                sp.GetRequiredService<ILogger<ScheduledTaskHost>>()
            ))
            .AddSingleton<IPluginStopping>(sp => sp.GetRequiredService<ScheduledTaskHost>())
            // The server's approvals: the plugins' handlers join ApprovalHandlers as they load.
            .AddSingleton(_ => new ApprovalHandlers([]))
            .AddSingleton(sp => new HotelApprovals(
                _serverDb,
                sp.GetRequiredService<ApprovalHandlers>(),
                sp,
                Fake.Clock,
                sp.GetRequiredService<ILogger<HotelApprovals>>()
            ))
            .AddSingleton<IApprovals>(sp => sp.GetRequiredService<HotelApprovals>())
            .AddSingleton<PluginTestDatabase>(new SqlitePluginDatabase(_db))
            .AddSingleton(
                new PluginTestHooks
                {
                    BuiltCode = m => _builds.GetValueOrDefault(m.Key),
                    Compiles = COMPILES,
                    Services = ChangePluginServices,
                    PluginHost = host => new StandInHost(host, Host, IsPluginType),
                }
            );

        // Core's command settings (Turbo:Commands), read as they are now: a test sets them at any time.
        services
            .AddOptions<CommandConfig>()
            .Bind(_configuration.GetSection(CommandConfig.SECTION_NAME));
        services.AddSingleton<IOptions<CommandConfig>>(sp => new CurrentOptions<CommandConfig>(
            sp.GetRequiredService<IOptionsMonitor<CommandConfig>>()
        ));

        _host = services.BuildServiceProvider();

        var sp = _host;

        Commands = sp.GetRequiredService<ICommandRegistryProvider>();
        Policies = sp.GetRequiredService<GamePolicies>();
        Packets = sp.GetRequiredService<MessageRegistry>();
        EventRegistry = sp.GetRequiredService<EventRegistry>();
        Schedules = sp.GetRequiredService<ScheduledTaskHost>();
        WebRoutes = sp.GetRequiredService<PluginWebRoutes>();
        AdminRoutes = sp.GetRequiredService<PluginAdminRoutes>();
        OperatorCommands = sp.GetRequiredService<IOperatorCommandRunner>();
        HotelApprovals = sp.GetRequiredService<HotelApprovals>();

        // Core's :confirm, which an operator command that reaches many players waits for.
        Commands.Register([new ConfirmCommand()]);

        // What the server's own feature processors find in a plugin's assembly, with the same
        // registries behind them (their refusals fail the load as on a server).
        IAssemblyFeatureProcessor[] processors =
        [
            new PluginMessageFeatureProcessor(
                Packets,
                sp.GetRequiredService<IPluginMessageDirectory>(),
                sp.GetRequiredService<ILoggerFactory>(),
                sp
            ),
            new MessageFeatureProcessor(
                Packets,
                sp.GetRequiredService<EnvelopeInvokerFactory<MessageContext>>(),
                sp.GetRequiredService<ILoggerFactory>(),
                sp
            ),
            new EventFeatureProcessor(
                sp.GetRequiredService<EventRegistry>(),
                sp.GetRequiredService<EnvelopeInvokerFactory<EventContext>>()
            ),
            new CommandFeatureProcessor(
                Commands,
                sp.GetRequiredService<SuggestionSourceRegistry>(),
                sp.GetRequiredService<CommandArgumentParserRegistry>()
            ),
            new PermissionNodeFeatureProcessor(
                sp.GetRequiredService<IPermissionRegistryProvider>()
            ),
            new RoomObjectLogicFeatureProcessor(sp.GetRequiredService<IRoomObjectLogicProvider>()),
            new Turbo.Rooms.Object.Logic.Plugins.PluginLogicFeatureProcessor(
                sp.GetRequiredService<IRoomObjectLogicProvider>()
            ),
            new RoomEventListenerFeatureProcessor(
                sp.GetRequiredService<IRoomEventListenerRegistry>()
            ),
            new ScheduledTaskFeatureProcessor(Schedules),
            new GamePolicyFeatureProcessor(Policies),
        ];

        _manager = new PluginManager(
            sp,
            new AssemblyProcessor(processors),
            Options.Create(
                new PluginConfig { PluginFolderPath = Path.Combine(_folder, "plugins") }
            ),
            sp.GetRequiredService<ILogger<PluginManager>>()
        );
    }

    /// <summary>A new hotel, its clock at <paramref name="now"/> (2026-01-01 12:00 UTC by default).</summary>
    public static TestHotel Create(DateTimeOffset? now = null) => new(now);

    /// <summary>The hotel API the plugins are given, with its records and assertions.</summary>
    public FakeHotel Fake { get; }

    /// <summary>The hotel's clock: a plugin's <see cref="TimeProvider"/> too. It moves when told.</summary>
    public ManualClock Clock => Fake.Clock;

    public FakeWallet Wallet => Fake.Wallet;

    public FakeInventory Inventory => Fake.Inventory;

    public FakeCatalog Catalog => Fake.Catalog;

    public FakeNotify Notify => Fake.Notify;

    public FakeWired Wired => Fake.Wired;

    /// <summary>The host services the kit stands in for, what was asked of them, and real ones to give.</summary>
    public StandIns Host { get; } = new();

    /// <summary>The hotel's settings, which plugins' settings classes are bound to.</summary>
    public TestSettings Settings { get; }

    /// <summary>What the hotel and the plugins logged.</summary>
    public TestLogs Logs { get; }

    /// <summary>Purchases, as the catalogue asks the plugins' purchase policies.</summary>
    public TestPurchases Purchases { get; }

    /// <summary>The plugins' scheduled tasks against <see cref="Clock"/>.</summary>
    public TestSchedule Schedule { get; }

    /// <summary>The plugins' player-facing web routes, called as a browser would.</summary>
    public TestWeb Web { get; }

    /// <summary>The plugins' admin panel endpoints, called as the panel would.</summary>
    public TestAdmin Admin { get; }

    /// <summary>Hotel events: what the plugins published, and events published to their handlers.</summary>
    public TestEvents Events { get; }

    /// <summary>
    /// The hotel's command log: the operator commands written down (by a holder of
    /// <c>command.log</c>, or a line that named a group of players).
    /// </summary>
    public TestCommandLog CommandLog { get; } = new();

    /// <summary>The hotel's language models, scripted (<see cref="AiFake"/>): a plugin declaring <c>"ai"</c> is allowed them.</summary>
    public AiFake Ai { get; }

    /// <summary>
    /// The requests for a person's approval the plugins asked for, approved or denied here as
    /// staff do in the panel, the plugins' handlers carrying them out.
    /// </summary>
    public TestApprovals Approvals { get; }

    /// <summary>The live plugins, by key.</summary>
    public IReadOnlyDictionary<string, TestPlugin> Plugins => _plugins;

    private readonly Dictionary<string, TestPlugin> _plugins = new(
        StringComparer.OrdinalIgnoreCase
    );

    internal StandIns Sessions { get; }

    internal ICommandRegistryProvider Commands { get; }

    internal GamePolicies Policies { get; }

    internal MessageRegistry Packets { get; }

    internal EventRegistry EventRegistry { get; }

    internal ScheduledTaskHost Schedules { get; }

    internal PluginWebRoutes WebRoutes { get; }

    internal PluginAdminRoutes AdminRoutes { get; }

    internal IOperatorCommandRunner OperatorCommands { get; }

    internal TestTexts Texts { get; }

    internal HotelApprovals HotelApprovals { get; }

    internal SqliteConnection Database => _db;

    /// <summary>
    /// Loads the plugin whose class (or any type of its assembly) is <typeparamref name="TPlugin"/>
    /// from its folder: the nearest folder holding a <c>manifest.json</c> above the test's own, or
    /// <paramref name="folder"/>. Its manifest, its <c>server/</c> sources (its manifest's
    /// <c>Sources</c>), <c>gamedata/</c> and <c>wwwroot/</c> are copied as a hotel installs them,
    /// compiled and held to the policy as the server does, so a plugin the server would refuse or
    /// that does not compile fails here with the server's reasons (<see cref="PluginLoadException"/>).
    /// It then runs as the test's build of the same sources, so the test shares its types.
    /// </summary>
    public Task<TestPlugin> LoadAsync<TPlugin>(
        string? folder = null,
        CancellationToken ct = default
    ) => LoadAsync(folder ?? FindPluginFolder(), typeof(TPlugin).Assembly, ct);

    /// <summary>
    /// Loads the plugin in <paramref name="folder"/> as <see cref="LoadAsync{TPlugin}"/> does,
    /// running <paramref name="build"/> (the plugin's assembly as the test built it); with none it
    /// runs what the server compiled, in a context of its own, whose types the test cannot name.
    /// </summary>
    public async Task<TestPlugin> LoadAsync(
        string folder,
        Assembly? build = null,
        CancellationToken ct = default
    )
    {
        var source = Path.GetFullPath(folder);
        var manifest = PluginHelpers.ReadManifest(source);
        var staged = Path.Combine(_folder, "plugins", manifest.Key);

        Stage(source, staged, manifest);

        if (build is not null)
            _builds[manifest.Key] = build;
        else
            _builds.TryRemove(manifest.Key, out _);

        try
        {
            await _manager.ReloadAsync(manifest.Key, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new PluginLoadException(manifest.Key, ex);
        }

        var plugin = new TestPlugin(
            manifest,
            _manager.AssemblyOf(manifest.Key) ?? throw new PluginLoadException(manifest.Key, null),
            _manager.ServicesOf(manifest.Key) ?? throw new PluginLoadException(manifest.Key, null),
            staged
        );

        _plugins[manifest.Key] = plugin;
        Schedule.Loaded(manifest.Key);

        return plugin;
    }

    /// <summary>Unloads a live plugin, as disabling it on a server does: its registrations go and its services stop.</summary>
    public async Task UnloadAsync(string key, CancellationToken ct = default)
    {
        Directory.Delete(Path.Combine(_folder, "plugins", key), recursive: true);
        await _manager.ReloadAsync(key, ct).ConfigureAwait(false);
        _plugins.Remove(key);
    }

    /// <summary>
    /// Gives every plugin loaded from now on <paramref name="service"/> as its own
    /// <typeparamref name="T"/>, in place of what it registers: a fake of its own gateway, say.
    /// </summary>
    public TestHotel Replace<T>(T service)
        where T : class
    {
        _replacements.Add(
            (_, services) =>
            {
                services.RemoveAll<T>();
                services.AddSingleton(service);
            }
        );

        return this;
    }

    /// <summary>The player called <paramref name="name"/>, online, added on first use.</summary>
    public TestPlayer Player(string name)
    {
        if (_players.TryGetValue(name, out var player))
            return player;

        return _players[name] = new TestPlayer(this, Fake.Players.Add(name, online: true));
    }

    /// <summary>Room <paramref name="id"/>, added on first use (owned by a player called "Owner").</summary>
    public TestRoom Room(int id, string? name = null)
    {
        if (_rooms.TryGetValue(id, out var room))
            return room;

        var existing = Fake.Rooms.All.FirstOrDefault(x => x.Id.Value == id);

        if (existing is null)
        {
            var next = Fake.Rooms.NextId;

            Fake.Rooms.NextId = id;
            existing = Fake.Rooms.Add(name ?? $"Room {id}", OwnerOfRooms());
            Fake.Rooms.NextId = Math.Max(next, id + 1);
        }

        return _rooms[id] = new TestRoom(this, existing);
    }

    internal TestPlayer PlayerOf(Turbo.Primitives.Players.PlayerId id) =>
        _players.Values.FirstOrDefault(x => x.Id == id)
        ?? throw new HotelAssertionException($"The test hotel has no player {id}.");

    internal IEnumerable<TestPlayer> AllPlayers => _players.Values;

    private FakePlayer OwnerOfRooms() =>
        Fake.Players.All.FirstOrDefault(x => x.Name == "Owner") ?? Player("Owner").Fake;

    internal Assembly? PluginAssemblyOf(Type type) =>
        _builds.Values.FirstOrDefault(asm => asm == type.Assembly);

    private bool IsPluginType(Type type)
    {
        if (type.HasElementType)
            return IsPluginType(type.GetElementType()!);

        return type.Assembly.IsCollectible
            || _builds.Values.Contains(type.Assembly)
            || (type.IsConstructedGenericType && type.GenericTypeArguments.Any(IsPluginType));
    }

    private void ChangePluginServices(PluginManifest manifest, IServiceCollection services)
    {
        // The plugin's clock is the hotel's.
        if (services.Any(d => d.ServiceType == typeof(TimeProvider)))
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Fake.Clock);
        }

        foreach (var replace in _replacements)
            replace(manifest, services);
    }

    /// <summary>The answers that make the stand-ins play this hotel: a player's permission nodes, a session's player.</summary>
    /// <summary>
    /// Whether the player holds <paramref name="node"/>: one granted them (<c>*</c> holds them
    /// all), or one a live plugin grants every rank by default (<c>GrantedByDefault</c>).
    /// </summary>
    internal bool Grants(FakePlayer player, string node) =>
        TestPlayer.Holds(player, node) || _defaultNodes.ContainsKey(node);

    // Core's nodes every rank holds by default (command.confirm, ...), then the live plugins'.
    private readonly ConcurrentDictionary<string, byte> _defaultNodes = new(
        new CorePermissionNodeSource()
            .Nodes.Where(n => n.GrantedByDefault)
            .Select(n => KeyValuePair.Create(n.Node, (byte)0)),
        StringComparer.Ordinal
    );

    private void AnswerHostCalls()
    {
        // A plugin's permission nodes: those granted by default are held by every player.
        Host.Answer<IPermissionRegistryProvider>(
            nameof(IPermissionRegistryProvider.Register),
            call =>
            {
                var nodes = ((IPermissionNodeSource)call.Args[0]!)
                    .Nodes.Where(n => n.GrantedByDefault)
                    .Select(n => n.Node)
                    .ToList();

                foreach (var node in nodes)
                    _defaultNodes[node] = 0;

                return new Released(() =>
                {
                    foreach (var node in nodes)
                        _defaultNodes.TryRemove(node, out _);
                });
            }
        );
        Host.Answer<IPlayerPermissionGrain>(
            nameof(IPlayerPermissionGrain.HasAsync),
            call =>
                Task.FromResult(
                    Fake.Players.All.FirstOrDefault(p => p.Id == TestPlayer.PlayerOf(call.Key))
                        is { } player
                        && Grants(player, (string)call.Args[0]!)
                )
        );
        // Who is online and players by name, as an operator command asks the hotel.
        Host.Answer<ISessionGateway>(
            nameof(ISessionGateway.GetOnlinePlayerIds),
            _ => Fake.Players.OnlineIds()
        );
        Host.Answer<IPlayerDirectoryGrain>(
            nameof(IPlayerDirectoryGrain.GetPlayerIdAsync),
            call =>
                Task.FromResult(
                    Fake.Players.All.FirstOrDefault(p =>
                        string.Equals(
                            p.Name,
                            (string)call.Args[0]!,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        is { } player
                        ? (Turbo.Primitives.Players.PlayerId?)player.Id
                        : null
                )
        );
        Host.Answer<IPlayerDirectoryGrain>(
            nameof(IPlayerDirectoryGrain.GetPlayerNameAsync),
            call =>
                Task.FromResult(
                    NameOf((Turbo.Primitives.Players.PlayerId)call.Args[0]!) ?? string.Empty
                )
        );
        Host.Answer<IPlayerDirectoryGrain>(
            nameof(IPlayerDirectoryGrain.GetPlayerNamesAsync),
            call =>
                Task.FromResult(
                    ((List<Turbo.Primitives.Players.PlayerId>)call.Args[0]!)
                        .Where(id => NameOf(id) is not null)
                        .Distinct()
                        .ToImmutableDictionary(id => id, id => NameOf(id)!)
                )
        );
        Host.Answer<ISessionGateway>(
            nameof(ISessionGateway.GetPlayerId),
            call =>
                _players.Values.FirstOrDefault(p => p.SessionKey == (SessionKey)call.Args[0]!)?.Id
                ?? Turbo.Primitives.Players.PlayerId.Invalid
        );
    }

    private string? NameOf(Turbo.Primitives.Players.PlayerId id) =>
        Fake.Players.All.FirstOrDefault(p => p.Id == id)?.Name;

    private static string FindPluginFolder()
    {
        for (
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            dir is not null;
            dir = dir.Parent
        )
        {
            if (File.Exists(Path.Combine(dir.FullName, "manifest.json")))
                return dir.FullName;
        }

        throw new InvalidOperationException(
            $"No folder above {AppContext.BaseDirectory} holds a manifest.json: name the plugin's folder (hotel.LoadAsync<MyPlugin>(folder))."
        );
    }

    /// <summary>The plugin's folder as a hotel installs it: its manifest, sources and files.</summary>
    private static void Stage(string source, string staged, PluginManifest manifest)
    {
        if (Directory.Exists(staged))
            Directory.Delete(staged, recursive: true);

        Directory.CreateDirectory(staged);
        File.Copy(Path.Combine(source, "manifest.json"), Path.Combine(staged, "manifest.json"));

        foreach (var file in PluginSourceFiles.Find(source, manifest))
            Copy(source, staged, file);

        foreach (var folder in (string[])["gamedata", "wwwroot"])
        {
            var from = Path.Combine(source, folder);

            if (!Directory.Exists(from))
                continue;

            foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
                Copy(source, staged, Path.GetRelativePath(source, file));
        }
    }

    private static void Copy(string source, string staged, string relative)
    {
        var to = Path.Combine(staged, relative);

        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Copy(Path.Combine(source, relative), to);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _manager.UnloadAllAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifetime.Dispose();
            await _host.DisposeAsync().ConfigureAwait(false);
            await _db.DisposeAsync().ConfigureAwait(false);
            _serverDb.Dispose();

            try
            {
                if (Directory.Exists(_folder))
                    Directory.Delete(_folder, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed class CurrentOptions<T>(IOptionsMonitor<T> monitor) : IOptions<T>
        where T : class
    {
        public T Value => monitor.CurrentValue;
    }

    private sealed class Released(Action release) : IDisposable
    {
        public void Dispose() => release();
    }

    /// <summary>The host's lifetime, started from the first: a plugin's hotel-ready hook runs as it loads.</summary>
    private sealed class TestLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public TestLifetime() => _started.Cancel();

        public CancellationToken ApplicationStarted => _started.Token;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => _stopped.Token;

        public void StopApplication() => _stopping.Cancel();

        public void Dispose()
        {
            _started.Dispose();
            _stopping.Dispose();
            _stopped.Dispose();
        }
    }

    /// <summary>
    /// The host's services as a plugin reaches them: the kit's, then a stand-in for any other
    /// interface (never one of a plugin's own types, nor a generic one such as <c>IOptions&lt;T&gt;</c>).
    /// </summary>
    private sealed class StandInHost(
        IServiceProvider host,
        StandIns standIns,
        Func<Type, bool> isPlugin
    ) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (host.GetService(serviceType) is { } service)
                return service;

            return
                serviceType.IsInterface
                && !serviceType.IsGenericType
                && !isPlugin(serviceType)
                && serviceType != typeof(IServiceProviderIsService)
                && serviceType.Namespace?.StartsWith("Microsoft.", StringComparison.Ordinal) != true
                ? standIns.Of(serviceType, null)
                : null;
        }
    }
}

/// <summary>
/// A plugin did not load, as a server would not load it: refused (an executable file, code the
/// policy forbids), did not compile, or failed as it started. <see cref="Reasons"/> are the
/// server's own lines.
/// </summary>
public sealed class PluginLoadException(string key, Exception? inner)
    : Exception(
        inner is null
            ? $"Plugin {key} did not load."
            : $"Plugin {key} did not load: {inner.Message}",
        inner
    )
{
    public string Key { get; } = key;

    /// <summary>Each compile error or refusal the server gave; the failure's message otherwise.</summary>
    public IReadOnlyList<string> Reasons { get; } =
        inner switch
        {
            Turbo.Plugins.Exceptions.PluginCompileException compile => compile.Errors,
            Turbo.Plugins.Exceptions.PluginRejectedException refused => refused.Reasons,
            null => [],
            _ => [inner.Message],
        };
}
