using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Campaign;
using Turbo.PacketHandlers.Handshake;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Campaigns;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Messages.Incoming.Campaign;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Campaign;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Settings;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Texts;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Admin;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// Host services a plugin replaces (<c>docs/plugin-services.md</c>): one the plugin registers in
/// its own services is what the host's handlers, its login and its admin API call while the plugin
/// is live, though each took the service by constructor before the plugin loaded, and the host's
/// own is back when it unloads. Two plugins replacing one are reported, the later one used.
/// </summary>
public sealed class ReplaceableServiceTests : IAsyncDisposable
{
    private const int PLAYER = 5;

    private readonly AdminPanelServer _panel = new();
    private readonly Fakes _fakes = new();
    private readonly CapturingLoggerFactory _hostLog = new();
    private readonly CapturingLogger<PluginManager> _managerLog = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-replaceable-services-" + Guid.NewGuid().ToString("N")
    );
    private PluginManager? _manager;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        if (_manager is { } manager)
            await manager.UnloadAllAsync(CancellationToken.None);

        await _panel.DisposeAsync();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task A_plugins_calendar_is_what_the_doors_the_login_and_the_admin_api_use_until_it_unloads()
    {
        _panel.Configure = services =>
            services.AddReplaceable<ICampaignCalendarService>(_ => new HostCalendar());
        _panel.Holds.Add(PermissionNodes.Admin.CAMPAIGNS_MANAGE);
        await _panel.StartAsync(Ct);
        var token = (await _panel.Auth.OpenSessionAsync(AdminPanelServer.STAFF, Ct)).SessionToken;

        // Taken by constructor before any plugin loads, as the host builds them at start.
        var calendars = _panel.Services.GetRequiredService<ICampaignCalendarService>();
        var door = ActivatorUtilities.CreateInstance<OpenCampaignCalendarDoorMessageHandler>(
            _panel.Services
        );
        var login = Login(calendars);

        (await OpenDoorAsync(door)).Should().Be("host");

        await LoadAsync(_panel.Services, ("advent", Calendar("advent")));

        (await OpenDoorAsync(door)).Should().Be("advent", "the door handler calls the plugin's");
        (await LogInAsync(login)).Should().Be("advent", "the login sends the plugin's calendar");
        (await DeleteAsync(token))
            .Should()
            .Be(HttpStatusCode.NoContent, "the admin API removes through the plugin's");
        _manager!
            .ContributionsOf("advent")!
            .Items.Should()
            .Contain(
                new PluginContribution(
                    PluginContributionKinds.SERVICE,
                    nameof(ICampaignCalendarService),
                    "Calendar"
                )
            );

        await _manager.UnloadAllAsync(Ct);

        (await OpenDoorAsync(door)).Should().Be("host");
        (await LogInAsync(login)).Should().Be("host");
        (await DeleteAsync(token)).Should().Be(HttpStatusCode.NotFound, "the host's has none");
        _panel.Services.GetRequiredService<IReplaceableServices>().Replacements.Should().BeEmpty();
    }

    [Fact]
    public async Task Two_plugins_replacing_one_service_are_a_conflict_and_the_one_under_comes_back()
    {
        var host = Host();

        await LoadAsync(host, ("first", Calendar("first")), ("second", Calendar("second")));

        var slot = host.GetRequiredService<IServiceSlot<ICampaignCalendarService>>();
        var replacements = host.GetRequiredService<IReplaceableServices>().Replacements;
        var top = replacements.Single(x => x.InUse).Owner;
        var under = replacements.Single(x => !x.InUse).Owner;

        slot.Owner.Should().Be(top);
        _hostLog
            .Entries.Should()
            .Contain(x =>
                x.Level == LogLevel.Warning
                && x.Message.StartsWith("Conflict: " + top, StringComparison.Ordinal)
            );
        _manager!
            .ContributionsOf(top)!
            .Conflicts.Should()
            .Contain($"{under} registers the service {nameof(ICampaignCalendarService)} too.");

        // Each adds to the one it replaced: the host's (none), the one under's, then its own.
        (await host.GetRequiredService<ICampaignCalendarService>().ListAsync(Ct))
            .Select(x => x.Name)
            .Should()
            .Equal(under, top);

        await _manager.DisableAsync(top, Ct);

        slot.Owner.Should().Be(under, "the plugin under it is used again, not the host's");
        (
            await host.GetRequiredService<ICampaignCalendarService>()
                .OpenDoorAsync(new PlayerId(PLAYER), "x", 0, false, Ct)
        )
            .ProductName.Should()
            .Be(under);
    }

    [Fact]
    public async Task A_plugin_registering_a_host_service_the_host_does_not_let_it_replace_is_told_so()
    {
        var host = Host(services => services.AddSingleton(_fakes.Create<IHotelTextProvider>()));

        await LoadAsync(
            host,
            (
                "texts",
                """
                using Microsoft.Extensions.DependencyInjection;
                using Turbo.Contracts.Plugins;
                using Turbo.Primitives.Texts;

                namespace texts;

                public sealed class TextsPlugin : TurboPlugin
                {
                    protected override void Services(IServiceCollection services) =>
                        services.AddSingleton<IHotelTextProvider>(_ => null!);
                }
                """
            )
        );

        _managerLog
            .AtLeast(LogLevel.Warning)
            .Should()
            .ContainSingle(x =>
                x.Message.Contains(nameof(IHotelTextProvider))
                && x.Message.Contains("does not let a plugin replace")
            );
    }

    [Fact]
    public void Every_replaceable_contract_is_registered_through_its_slot()
    {
        // As Program registers them: every host module over the shipped settings.
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            optional: false
        );
        var services = new ServiceCollection();

        foreach (var module in HostModules())
            module.ConfigureServices(services, builder);

        var contracts = new[] { typeof(IHostServices).Assembly, typeof(PlayerId).Assembly }
            .SelectMany(x => x.GetExportedTypes())
            .Where(x => x.IsInterface && x.IsDefined(typeof(ReplaceableServiceAttribute), false))
            .ToList();

        contracts
            .Should()
            .Contain([
                typeof(ICampaignCalendarService),
                typeof(Turbo.Primitives.Trading.IPlayerTradePolicy),
                typeof(Turbo.Primitives.Games.IGameAccessPolicy),
            ]);
        contracts
            .Where(contract => !ThroughItsSlot(services, contract))
            .Select(x => x.Name)
            .Should()
            .BeEmpty("a replaceable contract registered directly ignores a plugin's");
    }

    /// <summary>The last registration of the contract is the one AddReplaceable made.</summary>
    private static bool ThroughItsSlot(IServiceCollection services, Type contract)
    {
        var slot = typeof(IServiceSlot<>).MakeGenericType(contract);
        var last = services.LastOrDefault(x => x.ServiceType == contract && !x.IsKeyedService);

        return services.Any(x => x.ServiceType == slot)
            && last?.ImplementationFactory?.Method.DeclaringType?.Assembly
                == typeof(ReplaceableServiceExtensions).Assembly;
    }

    private static IEnumerable<IHostPluginModule> HostModules() =>
        Directory
            .EnumerateFiles(AppContext.BaseDirectory, "Turbo.*.dll")
            .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)))
            .SelectMany(x => x.GetTypes())
            .Where(x =>
                typeof(IHostPluginModule).IsAssignableFrom(x)
                && x is { IsAbstract: false, IsInterface: false }
                && x.GetConstructor(Type.EmptyTypes) is not null
                && x.Assembly != typeof(ReplaceableServiceTests).Assembly
            )
            .Select(x => (IHostPluginModule)Activator.CreateInstance(x)!);

    private ServiceProvider Host(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(_hostLog)
            .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
            .AddReplaceable<ICampaignCalendarService>(_ => new HostCalendar());

        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    private async Task LoadAsync(
        IServiceProvider host,
        params (string Key, string Source)[] plugins
    )
    {
        foreach (var (key, source) in plugins)
            PluginBuilder.Write(_folder, key, source);

        _manager = new PluginManager(
            host,
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            _managerLog
        );

        await _manager.LoadAllAsync(ct: Ct);

        _manager
            .Snapshot()
            .Should()
            .OnlyContain(x => x.State == PluginStateType.Loaded, "{0}", _manager.Snapshot());
    }

    /// <summary>
    /// A plugin's calendar, named for it: its doors give its name, it removes any calendar, and its
    /// list adds one named for it to the list of the calendar it replaced
    /// (<see cref="IServiceSlot{T}.Under"/>).
    /// </summary>
    private static string Calendar(string key) =>
        $$"""
            using System.Collections.Immutable;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using Turbo.Contracts.Plugins;
            using Turbo.Primitives.Campaigns;
            using Turbo.Primitives.Messages.Outgoing.Campaign;
            using Turbo.Primitives.Players;
            using Turbo.Primitives.Plugins;

            namespace {{key}};

            public sealed class Plugin : TurboPlugin
            {
                protected override void Services(IServiceCollection services) =>
                    services.AddSingleton<ICampaignCalendarService, Calendar>();
            }

            public sealed class Calendar(IServiceSlot<ICampaignCalendarService> slot) : ICampaignCalendarService
            {
                public Task<CampaignCalendarDataMessageComposer?> GetCalendarAsync(PlayerId playerId, CancellationToken ct) =>
                    Task.FromResult<CampaignCalendarDataMessageComposer?>(new()
                    {
                        CampaignName = "{{key}}",
                        CampaignImage = "",
                        CurrentDay = 0,
                        CampaignDays = 1,
                        OpenedDays = [],
                        MissedDays = [],
                    });

                public Task<CampaignCalendarDoorOpenedMessageComposer> OpenDoorAsync(
                    PlayerId playerId, string campaignName, int dayIndex, bool asStaff, CancellationToken ct
                ) => Task.FromResult(CampaignCalendarDoorOpenedMessageComposer.NotOpened with { DoorOpened = true, ProductName = "{{key}}" });

                public async Task<ImmutableArray<CampaignCalendarDefinition>> ListAsync(CancellationToken ct) =>
                    (await slot.Under(this).ListAsync(ct)).Add(new CampaignCalendarDefinition { Name = "{{key}}" });

                public Task<CampaignCalendarDefinition?> GetAsync(int id, CancellationToken ct) =>
                    Task.FromResult<CampaignCalendarDefinition?>(null);

                public Task<CampaignCalendarDefinition> SaveAsync(CampaignCalendarDefinition campaign, CancellationToken ct) =>
                    Task.FromResult(campaign);

                public Task<bool> DeleteAsync(int id, CancellationToken ct) => Task.FromResult(true);
            }
            """;

    private async Task<string> OpenDoorAsync(OpenCampaignCalendarDoorMessageHandler door)
    {
        var session = _fakes.Create<ISessionContext>("door");

        await door.HandleAsync(
            new OpenCampaignCalendarDoorMessage { CampaignName = "advent", DayIndex = 0 },
            new MessageContext(session, PLAYER, -1),
            Ct
        );

        return _fakes
            .Log.On<ISessionContext>()
            .Where(x => x.Method == "SendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<CampaignCalendarDoorOpenedMessageComposer>()
            .Last()
            .ProductName;
    }

    private SSOTicketMessageHandler Login(ICampaignCalendarService calendars)
    {
        _fakes.Handlers["GetPlayerIdFromTicketAsync"] = _ => Task.FromResult(PLAYER);
        _fakes.Handlers["GetOwnedAsync"] = _ => Task.FromResult(ImmutableHashSet<int>.Empty);
        _fakes.Handlers["AdmitsAsync"] = _ => Task.FromResult(true);
        _fakes.Handlers["GetLoginActionsAsync"] = _ => Task.FromResult(ImmutableArray<short>.Empty);
        _fakes.Handlers["GetSettingsAsync"] = _ =>
            Task.FromResult(Activator.CreateInstance<PlayerSettingsSnapshot>());
        _fakes.Handlers["GetFavouriteRoomIdsAsync"] = _ =>
            Task.FromResult(ImmutableArray<RoomId>.Empty);
        _fakes.Handlers["GetClubGiftInfoAsync"] = _ => Task.FromResult(ClubGiftInfoSnapshot.Empty);
        _fakes.Handlers["GetUnseenItemsAsync"] = _ =>
            Task.FromResult(ImmutableDictionary<UnseenItemCategory, ImmutableArray<int>>.Empty);
        _fakes.Handlers["GetSummaryAsync"] = _ =>
            Task.FromResult(Activator.CreateInstance<PlayerSummarySnapshot>());
        _fakes.Handlers[nameof(ICallForHelpService.GetTopicsAsync)] = _ =>
            Task.FromResult(ImmutableArray<CfhCategorySnapshot>.Empty);
        _fakes.Handlers["GetMessageAsync"] = _ => Task.FromResult(string.Empty);
        HotelTextFakes.Use(_fakes, _ => null);

        return new SSOTicketMessageHandler(
            _fakes.Create<IAuthenticationService>(),
            _fakes.Create<ISessionGateway>(),
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<INavigatorService>(),
            _fakes.Create<ISanctionService>(),
            _fakes.Create<IHotelAvailability>(),
            _fakes.Create<IHotelTextProvider>(),
            _fakes.Create<Turbo.Primitives.Figures.IPlayerClothingService>(),
            _fakes.Create<ICallForHelpService>(),
            _fakes.Create<Turbo.Primitives.Players.Onboarding.IPlayerOnboardingService>(),
            _fakes.Create<IModerationToolService>(),
            _fakes.Create<IModerationIssueService>(),
            _fakes.Create<Turbo.Primitives.Players.Notifications.IPlayerNoticeService>(),
            _fakes.Create<Turbo.Primitives.Tracking.IClientTelemetry>(),
            new Turbo.Authentication.PlayPolicies(
                NullLogger<Turbo.Authentication.PlayPolicies>.Instance
            ),
            calendars
        );
    }

    /// <summary>The campaign of the calendar the login sent.</summary>
    private async Task<string> LogInAsync(SSOTicketMessageHandler login)
    {
        var session = _fakes.Create<ISessionContext>("login-" + Guid.NewGuid().ToString("N"));

        await login.HandleAsync(
            new SSOTicketMessage { SSO = "ticket" },
            new MessageContext(session, 0, -1),
            Ct
        );

        return _fakes
            .Log.On<ISessionContext>()
            .Where(x => x.Method == "SendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<CampaignCalendarDataMessageComposer>()
            .Last()
            .CampaignName;
    }

    private async Task<HttpStatusCode> DeleteAsync(string token) =>
        (
            await _panel.SendAsync(HttpMethod.Delete, "/api/campaign-calendars/5", token, Ct)
        ).StatusCode;

    /// <summary>The host's own calendar: named "host", with no calendar to remove.</summary>
    private sealed class HostCalendar : ICampaignCalendarService
    {
        public Task<CampaignCalendarDataMessageComposer?> GetCalendarAsync(
            PlayerId playerId,
            CancellationToken ct
        ) =>
            Task.FromResult<CampaignCalendarDataMessageComposer?>(
                new()
                {
                    CampaignName = "host",
                    CampaignImage = "",
                    CurrentDay = 0,
                    CampaignDays = 1,
                    OpenedDays = [],
                    MissedDays = [],
                }
            );

        public Task<CampaignCalendarDoorOpenedMessageComposer> OpenDoorAsync(
            PlayerId playerId,
            string campaignName,
            int dayIndex,
            bool asStaff,
            CancellationToken ct
        ) =>
            Task.FromResult(
                CampaignCalendarDoorOpenedMessageComposer.NotOpened with
                {
                    ProductName = "host",
                }
            );

        public Task<ImmutableArray<CampaignCalendarDefinition>> ListAsync(CancellationToken ct) =>
            Task.FromResult(ImmutableArray<CampaignCalendarDefinition>.Empty);

        public Task<CampaignCalendarDefinition?> GetAsync(int id, CancellationToken ct) =>
            Task.FromResult<CampaignCalendarDefinition?>(null);

        public Task<CampaignCalendarDefinition> SaveAsync(
            CampaignCalendarDefinition campaign,
            CancellationToken ct
        ) => Task.FromResult(campaign);

        public Task<bool> DeleteAsync(int id, CancellationToken ct) => Task.FromResult(false);
    }
}
