using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Turbo.Messages.Registry;
using Turbo.Networking.Extensions;
using Turbo.Operations;
using Turbo.Operations.Configuration;
using Turbo.PacketHandlers.Turbo;
using Turbo.Primitives.Availability;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Players;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>
/// The notice players see while a restart, a shutdown or a maintenance counts down: off until the
/// hotel turns it on for the kind, then put up with the countdown's end and link, again at each of
/// its points, and taken down when the countdown is called off or runs out. Habbo's own closing
/// alerts are sent as before, whatever the notice does.
/// </summary>
public class HotelCountdownNoticeServiceTests
{
    private static readonly DateTime START = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private const int PLAYER = 10;

    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _clock = new(START);
    private readonly TestOptionsMonitor<OperationsConfig> _config = new(new OperationsConfig());
    private readonly Dictionary<string, string> _texts = [];
    private readonly HotelAvailabilityService _availability;
    private readonly HotelCountdownNoticeService _notice;

    public HotelCountdownNoticeServiceTests()
    {
        _fakes.Handlers["GetOnlinePlayerIds"] = _ =>
            (IReadOnlyCollection<PlayerId>)new List<PlayerId> { PLAYER };
        _fakes.Handlers["GetTextAsync"] = call =>
            Task.FromResult(_texts.GetValueOrDefault((string)call.Args[0]!));

        _availability = new HotelAvailabilityService(
            _config,
            _clock,
            _fakes.Create<ISessionGateway>(),
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<IHotelTextProvider>(),
            _fakes.Create<IHostApplicationLifetime>(),
            new CapturingLogger<IHotelAvailability>()
        );
        _notice = new HotelCountdownNoticeService(
            _config,
            _availability,
            _clock,
            _fakes.Create<ISessionGateway>(),
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<IHotelTextProvider>(),
            new CapturingLogger<IHotelCountdownNotice>()
        );
    }

    private void TurnOn(
        Func<CountdownNoticeConfig, CountdownNoticeConfig>? restart = null,
        Func<CountdownNoticeConfig, CountdownNoticeConfig>? maintenance = null
    ) =>
        _config.Set(
            new OperationsConfig
            {
                RestartNotice = (restart ?? (x => x))(new CountdownNoticeConfig { Enabled = true }),
                MaintenanceNotice = (maintenance ?? (x => x))(new CountdownNoticeConfig()),
            }
        );

    /// <summary>Both loops, as the host runs them.</summary>
    private async Task TickAsync()
    {
        await _availability.TickAsync(CancellationToken.None);
        await _notice.SyncAsync(CancellationToken.None);
    }

    private List<TurboNoticeMessage> Notices() =>
        [.. _fakes.SentToPlayer<TurboNoticeMessage>(PLAYER)];

    [Fact]
    public async Task A_hotel_that_never_turned_it_on_shows_no_notice_only_Habbos_own_alert()
    {
        _availability.ScheduleRestart(TimeSpan.FromMinutes(3), "Update").Should().BeTrue();

        await TickAsync();

        Notices().Should().BeEmpty();
        _notice.Current.Should().BeNull();
        _fakes.SentToPlayer<InfoHotelClosingMessageComposer>(PLAYER).Should().ContainSingle();
    }

    [Fact]
    public async Task A_restart_puts_its_countdown_notice_up_once_with_the_end_and_the_default_link()
    {
        TurnOn(x => new CountdownNoticeConfig
        {
            Enabled = true,
            DefaultLink = "https://hotel.example/changelog",
        });
        _availability.ScheduleRestart(TimeSpan.FromMinutes(3), "Update");

        await TickAsync();
        await TickAsync();

        var notice = Notices().Should().ContainSingle().Which;

        notice.Key.Should().Be("countdown_restart");
        notice.Shown.Should().BeTrue();
        notice.Description.Should().Be("Hotel restart in");
        notice
            .Variables.Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["type"] = "countdown",
                    ["count_down_to"] = "2026-10-21 12:03:00",
                    ["expiry"] = "2026-10-21 12:03:00",
                    ["external_link"] = "https://hotel.example/changelog",
                }
            );
        _notice.Current.Should().Be(notice);
        // Habbo's own alert is untouched: one, with the minutes.
        _fakes
            .SentToPlayer<InfoHotelClosingMessageComposer>(PLAYER)
            .Should()
            .ContainSingle()
            .Which.MinutesUntilClosing.Should()
            .Be(3);
    }

    [Fact]
    public async Task The_restarts_own_link_wins_and_the_hotel_text_fills_its_placeholders()
    {
        TurnOn(x => new CountdownNoticeConfig
        {
            Enabled = true,
            DefaultLink = "https://hotel.example/changelog",
            Dismissible = false,
        });
        _texts["hotel.countdown_notice.restart"] = "{reason} at {time} ({minutes} min)";
        _availability.ScheduleRestart(TimeSpan.FromMinutes(3), "Update", "habbopages/news");

        await TickAsync();

        var notice = Notices().Should().ContainSingle().Which;

        notice.Description.Should().Be("Update at 12:03 (3 min)");
        notice.Variables.Should().Contain("internal_link", "habbopages/news");
        notice.Variables.Should().NotContainKey("external_link");
        notice.Variables.Should().Contain("close", "never");
    }

    [Fact]
    public async Task Calling_the_restart_off_takes_the_notice_down()
    {
        TurnOn();
        _availability.ScheduleRestart(TimeSpan.FromMinutes(3), string.Empty);
        await TickAsync();

        _availability.Cancel().Should().BeTrue();
        await TickAsync();
        await TickAsync();

        Notices()
            .Select(x => (x.Key, x.Shown))
            .Should()
            .Equal(("countdown_restart", true), ("countdown_restart", false));
        _notice.Current.Should().BeNull();
    }

    [Fact]
    public async Task A_countdown_that_runs_out_takes_its_notice_down()
    {
        TurnOn(maintenance: _ => new CountdownNoticeConfig { Enabled = true });
        _availability.ScheduleMaintenance(TimeSpan.FromMinutes(1), string.Empty);
        await TickAsync();

        _clock.Advance(TimeSpan.FromMinutes(1));
        await TickAsync();

        Notices()
            .Select(x => (x.Key, x.Shown))
            .Should()
            .Equal(("countdown_maintenance", true), ("countdown_maintenance", false));
        Notices()[0].Description.Should().Be("Maintenance break in");
    }

    [Fact]
    public async Task It_is_put_up_at_its_first_point_and_again_at_each_later_one()
    {
        TurnOn(x => new CountdownNoticeConfig { Enabled = true, PostAtMinutes = [5, 1] });
        _availability.ScheduleRestart(TimeSpan.FromMinutes(10), string.Empty);

        await TickAsync();
        Notices().Should().BeEmpty();

        _clock.Advance(TimeSpan.FromSeconds(5 * 60 + 1)); // 4:59 left
        await TickAsync();
        await TickAsync();
        Notices().Should().ContainSingle();

        _clock.Advance(TimeSpan.FromMinutes(4)); // 0:59 left
        await TickAsync();
        Notices().Should().HaveCount(2).And.OnlyContain(x => x.Shown);
    }

    [Fact]
    public async Task Turning_a_kind_off_takes_its_notice_down_and_other_kinds_stay_off()
    {
        TurnOn();
        _availability.ScheduleShutdown(TimeSpan.FromMinutes(5), string.Empty);
        await TickAsync();
        Notices().Should().BeEmpty();

        _availability.ScheduleRestart(TimeSpan.FromMinutes(5), string.Empty).Should().BeFalse();
        _availability.Cancel();
        _availability.ScheduleRestart(TimeSpan.FromMinutes(5), string.Empty);
        await TickAsync();
        Notices().Should().ContainSingle();

        _config.Set(new OperationsConfig());
        await TickAsync();

        Notices().Select(x => x.Shown).Should().Equal(true, false);
    }

    [Fact]
    public async Task A_client_that_asks_for_live_notices_is_sent_the_one_up_now()
    {
        TurnOn();
        _availability.ScheduleRestart(TimeSpan.FromMinutes(3), string.Empty);
        await _notice.SyncAsync(CancellationToken.None);
        _fakes.Log.Calls.Clear();

        var handler = new TurboClientCapabilitiesMessageHandler(
            _fakes.Create<Orleans.IGrainFactory>(),
            new ExtensionPacketRegistry(),
            _fakes.Create<ICommandTreeService>(),
            _notice,
            _fakes.Create<IClientModuleDirectory>()
        );

        await handler.HandleAsync(
            new TurboClientCapabilitiesMessage
            {
                Capabilities =
                [
                    new ClientCapabilitySnapshot
                    {
                        Name = ClientCapabilities.LIVE_NOTICES,
                        Version = 1,
                    },
                ],
            },
            new MessageContext(_fakes.Create<ISessionContext>("session"), PLAYER, -1),
            CancellationToken.None
        );

        Notices().Should().ContainSingle().Which.Should().Be(_notice.Current);
    }
}
