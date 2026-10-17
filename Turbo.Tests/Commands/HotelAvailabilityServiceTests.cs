using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Turbo.Operations;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

public class HotelAvailabilityServiceTests
{
    private static readonly DateTime START = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private const int GUEST = 10;
    private const int STAFF = 11;

    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _clock = new(START);
    private readonly TestOptionsMonitor<OperationsConfig> _config = new(new OperationsConfig());
    private readonly HotelAvailabilityService _service;
    private readonly List<int> _disconnected = [];
    private readonly Dictionary<int, IComposer?> _farewells = [];

    public HotelAvailabilityServiceTests()
    {
        _fakes.Handlers["GetOnlinePlayerIds"] = _ =>
            (IReadOnlyCollection<PlayerId>)new List<PlayerId> { GUEST, STAFF };
        _fakes.Handlers["DisconnectPlayerAsync"] = call =>
        {
            var id = ((PlayerId)call.Args[0]!).Value;

            _disconnected.Add(id);
            _farewells[id] = (IComposer?)call.Args[1];

            return Task.FromResult(true);
        };
        _fakes.Handlers["HasAsync"] = call =>
            call.Interface == typeof(IPlayerPermissionGrain)
                ? Task.FromResult(
                    (long)call.Key! == STAFF
                        && (string)call.Args[0]! == PermissionNodes.Hotel.MAINTENANCE_BYPASS
                )
                : Fakes.NotHandled;

        _service = new HotelAvailabilityService(
            _config,
            _clock,
            _fakes.Create<ISessionGateway>(),
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<IHotelTextProvider>(),
            _fakes.Create<IHostApplicationLifetime>(),
            new CapturingLogger<IHotelAvailability>()
        );
    }

    private Task TickAsync() => _service.TickAsync(CancellationToken.None);

    /// <summary>What one player was sent, in order.</summary>
    private IEnumerable<object?> SentTo(int id) =>
        _fakes.Log.Of("SendComposerAsync").Where(x => (long)x.Key! == id).Select(x => x.Args[0]);

    [Fact]
    public void AHotelThatNobodyTouched_IsOpen_AndLetsEveryoneIn()
    {
        _service.Current.Phase.Should().Be(HotelAvailabilityPhase.Open);
        _service.Current.BlocksLogin.Should().BeFalse();
    }

    [Fact]
    public async Task AMaintenanceCountdown_TellsTheHotelAtOnce_ThenAtEachReminderItReaches()
    {
        _service
            .ScheduleMaintenance(TimeSpan.FromMinutes(10), "database upgrade")
            .Should()
            .BeTrue();
        _service.Current.Phase.Should().Be(HotelAvailabilityPhase.MaintenanceScheduled);

        await TickAsync();

        var first = SentTo(GUEST)
            .OfType<MaintenanceStatusMessageComposer>()
            .Should()
            .ContainSingle();
        first
            .Which.Should()
            .BeEquivalentTo(new { IsInMaintenance = false, MinutesUntilMaintenance = 10 });
        SentTo(GUEST)
            .OfType<HabboBroadcastMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Be("database upgrade");

        // The same instant again says nothing new.
        await TickAsync();
        SentTo(GUEST).OfType<MaintenanceStatusMessageComposer>().Should().HaveCount(1);

        _clock.Advance(TimeSpan.FromSeconds(5 * 60 + 1)); // 4:59 left: the five minute reminder
        await TickAsync();
        await TickAsync();

        SentTo(GUEST)
            .OfType<MaintenanceStatusMessageComposer>()
            .Select(x => x.MinutesUntilMaintenance)
            .Should()
            .Equal(10, 5);
    }

    [Fact]
    public async Task ACountdownShorterThanAReminder_DoesNotMakeThatReminderUp()
    {
        _service.ScheduleMaintenance(TimeSpan.FromMinutes(3), string.Empty);

        await TickAsync();
        _clock.Advance(TimeSpan.FromSeconds(30)); // 2:30 left: nothing new yet
        await TickAsync();
        _clock.Advance(TimeSpan.FromSeconds(31)); // 1:59 left: the two minute reminder
        await TickAsync();

        SentTo(GUEST)
            .OfType<MaintenanceStatusMessageComposer>()
            .Select(x => x.MinutesUntilMaintenance)
            .Should()
            .Equal(3, 2);
    }

    [Fact]
    public async Task WhenTheCountdownRunsOut_TheHotelClosesToThoseWhoMayNotStay()
    {
        _service.ScheduleMaintenance(TimeSpan.FromMinutes(1), string.Empty);
        await TickAsync();

        _clock.Advance(TimeSpan.FromMinutes(1));
        await TickAsync();

        _service.Current.Phase.Should().Be(HotelAvailabilityPhase.Maintenance);
        _service.Current.BlocksLogin.Should().BeTrue();
        _disconnected.Should().Equal(GUEST);
        _farewells[GUEST]
            .Should()
            .BeOfType<HabboBroadcastMessageComposer>()
            .Which.Message.Should()
            .Be("The hotel is now in maintenance. Please come back later.");
    }

    [Fact]
    public async Task InMaintenance_OnlyThoseWithTheBypassNodeAreAdmitted()
    {
        (await _service.AdmitsAsync(GUEST, CancellationToken.None)).Should().BeTrue();

        _service.ScheduleMaintenance(TimeSpan.Zero, string.Empty);
        await TickAsync();

        (await _service.AdmitsAsync(GUEST, CancellationToken.None)).Should().BeFalse();
        (await _service.AdmitsAsync(STAFF, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task EndingMaintenance_OpensTheHotelAgain()
    {
        _service.ScheduleMaintenance(TimeSpan.Zero, string.Empty);
        await TickAsync();

        _service.Cancel().Should().BeTrue();

        _service.Current.Phase.Should().Be(HotelAvailabilityPhase.Open);
        (await _service.AdmitsAsync(GUEST, CancellationToken.None)).Should().BeTrue();
        _service.Cancel().Should().BeFalse();
    }

    [Fact]
    public async Task ACountdownCalledOff_NeverFires()
    {
        _service.ScheduleMaintenance(TimeSpan.FromMinutes(1), string.Empty);
        await TickAsync();

        _service.Cancel();
        _clock.Advance(TimeSpan.FromMinutes(5));
        await TickAsync();

        _service.Current.Phase.Should().Be(HotelAvailabilityPhase.Open);
        _disconnected.Should().BeEmpty();
    }

    [Fact]
    public async Task AShutdownCountdown_RemindsWithTheClosingNotice_ThenSendsEveryoneHomeAndStopsTheHost()
    {
        _service.ScheduleShutdown(TimeSpan.FromMinutes(2), string.Empty);

        await TickAsync();

        SentTo(STAFF)
            .OfType<InfoHotelClosingMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.MinutesUntilClosing.Should()
            .Be(2);

        _clock.Advance(TimeSpan.FromMinutes(2));
        await TickAsync();

        _disconnected.Should().BeEquivalentTo([GUEST, STAFF]);
        _fakes.Log.Of("StopApplication").Should().ContainSingle();

        // The host takes a moment to stop: the loop's next looks at the clock do nothing more, and
        // nobody, staff included, is let in or can call it off.
        await TickAsync();
        await TickAsync();

        _fakes.Log.Of("StopApplication").Should().ContainSingle();
        _service.Current.Phase.Should().Be(HotelAvailabilityPhase.ShuttingDown);
        (await _service.AdmitsAsync(STAFF, CancellationToken.None)).Should().BeFalse();
        _service.Cancel().Should().BeFalse();
        _service.ScheduleMaintenance(TimeSpan.FromMinutes(1), string.Empty).Should().BeFalse();
    }

    [Fact]
    public void AShutdown_WinsOverAMaintenance_AndReplacesOneAlreadyCountingDown()
    {
        _service.ScheduleMaintenance(TimeSpan.FromMinutes(5), string.Empty);

        _service.ScheduleShutdown(TimeSpan.FromMinutes(5), string.Empty);

        _service.Current.Phase.Should().Be(HotelAvailabilityPhase.ShutdownScheduled);
        _service.ScheduleMaintenance(TimeSpan.FromMinutes(1), string.Empty).Should().BeFalse();
        _service.Current.Phase.Should().Be(HotelAvailabilityPhase.ShutdownScheduled);
    }

    [Fact]
    public async Task ARestartCountdown_WarnsLikeAShutdown_ThenSendsEveryoneHome_AndEndsWithTheRestartExitCode()
    {
        _service.ScheduleRestart(TimeSpan.FromMinutes(2), "Applying settings").Should().BeTrue();

        await TickAsync();

        _service.Current.Restart.Should().BeTrue();
        SentTo(GUEST)
            .OfType<InfoHotelClosingMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.MinutesUntilClosing.Should()
            .Be(2);
        SentTo(GUEST)
            .OfType<HabboBroadcastMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Be("Applying settings");
        _service.ExitCode.Should().Be(0);

        _clock.Advance(TimeSpan.FromMinutes(2));
        await TickAsync();

        _disconnected.Should().BeEquivalentTo([GUEST, STAFF]);
        _farewells[GUEST]
            .Should()
            .BeOfType<HabboBroadcastMessageComposer>()
            .Which.Message.Should()
            .Contain("restarting");
        _fakes.Log.Of("StopApplication").Should().ContainSingle();
        _service.Current.Phase.Should().Be(HotelAvailabilityPhase.ShuttingDown);
        _service.ExitCode.Should().Be(new OperationsConfig().RestartExitCode);
        (await _service.AdmitsAsync(STAFF, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task AShutdown_EndsWithZero_AndARestartDoesNotOverruleIt()
    {
        _service.ScheduleShutdown(TimeSpan.FromMinutes(5), string.Empty);

        _service.ScheduleRestart(TimeSpan.FromMinutes(1), string.Empty).Should().BeFalse();
        _service.Current.Restart.Should().BeFalse();

        _clock.Advance(TimeSpan.FromMinutes(5));
        await TickAsync();

        _fakes.Log.Of("StopApplication").Should().ContainSingle();
        _service.ExitCode.Should().Be(0);
        _service.ScheduleRestart(TimeSpan.FromMinutes(1), string.Empty).Should().BeFalse();
    }

    [Fact]
    public void AShutdownReplacesARestart_AndARestartReplacesAMaintenance()
    {
        _service.ScheduleMaintenance(TimeSpan.FromMinutes(5), string.Empty);
        _service.ScheduleRestart(TimeSpan.FromMinutes(5), string.Empty).Should().BeTrue();

        _service.Current.Phase.Should().Be(HotelAvailabilityPhase.ShutdownScheduled);
        _service.Current.Restart.Should().BeTrue();

        _service.ScheduleShutdown(TimeSpan.FromMinutes(5), string.Empty);

        _service.Current.Restart.Should().BeFalse();
    }

    [Fact]
    public async Task ACalledOffRestart_NeverStopsTheHost()
    {
        _service.ScheduleRestart(TimeSpan.FromMinutes(1), string.Empty);

        _service.Cancel().Should().BeTrue();
        _clock.Advance(TimeSpan.FromMinutes(1));
        await TickAsync();

        _fakes.Log.Of("StopApplication").Should().BeEmpty();
        _service.Current.Should().Be(HotelAvailabilitySnapshot.Open);
        _service.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task TheRestartExitCode_AndTheReminders_AreReadAsTheyAreUsed()
    {
        _service.ScheduleRestart(TimeSpan.FromMinutes(10), string.Empty);
        await TickAsync();

        // Changed in the panel while the countdown runs: it applies to this one.
        _config.Set(new OperationsConfig { CountdownReminderMinutes = [7], RestartExitCode = 42 });

        _clock.Advance(TimeSpan.FromMinutes(3));
        await TickAsync();

        SentTo(GUEST)
            .OfType<InfoHotelClosingMessageComposer>()
            .Select(x => x.MinutesUntilClosing)
            .Should()
            .Equal(10, 7);

        _clock.Advance(TimeSpan.FromMinutes(7));
        await TickAsync();

        _service.ExitCode.Should().Be(42);
    }
}
