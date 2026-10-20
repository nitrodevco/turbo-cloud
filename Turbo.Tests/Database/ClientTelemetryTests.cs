using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Configuration;
using Turbo.Database.Entities.Tracking;
using Turbo.Database.Tracking;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Messages.Incoming.Tracking;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Tracking;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// What clients report about themselves is kept (<see cref="ClientTelemetryRecorder"/>): the
/// official PerformanceLog, LatencyPingReport, LagWarningReport and EventLog, as their bytes
/// arrive, and the login steps the server sees. Reports are bounded per player and by the queue,
/// never wait on the database, and expire into daily figures (<see cref="ClientTelemetryMaintenance"/>).
/// </summary>
public sealed class ClientTelemetryTests : IDisposable
{
    private const int PLAYER = 5;
    private const int OTHER = 6;
    private const int ROOM = 77;
    private const string CHROME_WINDOWS =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";
    private const string SAFARI_IPHONE =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1";

    private readonly SqliteDb _db = new();
    private readonly ManualTimeProvider _time = new(
        new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero)
    );
    private readonly TestOptionsMonitor<ClientTelemetryConfig> _config = new(
        new ClientTelemetryConfig()
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_performance_report_is_kept_with_its_device_and_room_and_no_address()
    {
        var recorder = Recorder();
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(IClientTelemetry)] = recorder;

        // PerformanceTracker.as:156 sends (seconds, user agent, flash version, os, browser,
        // debugger, memory KB, -1, garbage collections, average frame interval ms, slow frames).
        await harness.SendAsync(
            PacketHarness.Incoming("PerformanceLogMessageEvent"),
            PacketHarness.Payload(w =>
                w.Int(120)
                    .String(CHROME_WINDOWS)
                    .String("WIN 32,0,0,0")
                    .String("Windows 10")
                    .String("")
                    .Bool(false)
                    .Int(262_144)
                    .Int(-1)
                    .Int(2)
                    .Int(16)
                    .Int(3)
            ),
            PLAYER,
            ROOM
        );
        await recorder.FlushAsync(Ct);

        var row = (await Rows<ClientPerformanceLogEntity>()).Should().ContainSingle().Subject;
        row.PlayerId.Should().Be(PLAYER);
        row.RoomId.Should().Be(ROOM);
        row.ElapsedSeconds.Should().Be(120);
        (row.BrowserFamily, row.OsFamily, row.DeviceType)
            .Should()
            .Be(("Chrome", "Windows", "desktop"));
        row.MemoryKb.Should().Be(262_144);
        row.GarbageCollections.Should().Be(2);
        row.AverageUpdateIntervalMs.Should().Be(16);
        row.SlowUpdates.Should().Be(3);
        typeof(ClientPerformanceLogEntity)
            .GetProperties()
            .Select(x => x.Name)
            .Should()
            .NotContain(x => x.Contains("IP", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_latency_report_is_read_whole()
    {
        var recorder = Recorder();
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(IClientTelemetry)] = recorder;

        // An official client capture: 000000630000006300000003 = average 99, without spikes 99, 3 pings.
        await harness.SendAsync(
            PacketHarness.Incoming("LatencyPingReportMessageEvent"),
            PacketHarness.Payload(w => w.Int(99).Int(99).Int(3)),
            PLAYER
        );
        await recorder.FlushAsync(Ct);

        var row = (await Rows<ClientLatencyReportEntity>()).Should().ContainSingle().Subject;
        (row.AverageMs, row.ValidAverageMs, row.PingCount).Should().Be((99, 99, 3));
        row.RoomId.Should().BeNull();
        row.BrowserFamily.Should().Be(ClientUserAgents.UNKNOWN);
    }

    [Fact]
    public async Task Lag_warnings_and_events_are_kept_as_sent()
    {
        var recorder = Recorder();
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(IClientTelemetry)] = recorder;

        await harness.SendAsync(
            PacketHarness.Incoming("LagWarningReportMessageEvent"),
            PacketHarness.Payload(w => w.Int(4)),
            PLAYER,
            ROOM
        );
        // An official client capture: Navigation / Room Forward / go.roomforward / "" / 80462556.
        await harness.SendAsync(
            PacketHarness.Incoming("EventLogMessageEvent"),
            PacketHarness.Payload(w =>
                w.String("Navigation")
                    .String("Room Forward")
                    .String("go.roomforward")
                    .String("")
                    .Int(80462556)
            ),
            PLAYER,
            ROOM
        );
        await recorder.FlushAsync(Ct);

        (await Rows<ClientLagWarningEntity>())
            .Should()
            .ContainSingle(x => x.WarningCount == 4 && x.RoomId == ROOM);
        var ev = (await Rows<ClientEventLogEntity>()).Should().ContainSingle().Subject;
        (ev.Category, ev.Type, ev.Action, ev.ExtraString, ev.ExtraInt)
            .Should()
            .Be(("Navigation", "Room Forward", "go.roomforward", "", 80462556));
    }

    [Fact]
    public async Task A_latency_report_takes_the_device_of_its_sessions_last_performance_report()
    {
        var recorder = Recorder();

        recorder.RecordPerformance("s1", PLAYER, -1, Performance(SAFARI_IPHONE, 33));
        recorder.RecordLatency(
            "s1",
            PLAYER,
            ROOM,
            new LatencyPingReportMessage { AverageLatency = 80 }
        );
        recorder.RecordLatency(
            "s2",
            OTHER,
            ROOM,
            new LatencyPingReportMessage { AverageLatency = 90 }
        );
        await recorder.FlushAsync(Ct);

        var rows = await Rows<ClientLatencyReportEntity>();
        rows.Single(x => x.PlayerId == PLAYER)
            .Should()
            .Match<ClientLatencyReportEntity>(x =>
                x.BrowserFamily == "Safari" && x.OsFamily == "iOS" && x.DeviceType == "mobile"
            );
        rows.Single(x => x.PlayerId == OTHER).BrowserFamily.Should().Be(ClientUserAgents.UNKNOWN);
    }

    [Fact]
    public async Task Reports_over_a_players_per_minute_limit_are_dropped_until_the_next_minute()
    {
        _config.Set(new ClientTelemetryConfig { MaxEventsPerPlayerPerMinute = 3 });
        var recorder = Recorder();

        for (var i = 0; i < 5; i++)
            recorder.RecordEvent(PLAYER, ROOM, Event("Navigation", "go.official"));

        recorder.RecordEvent(OTHER, ROOM, Event("Navigation", "go.official"));
        _time.Advance(TimeSpan.FromMinutes(1));
        recorder.RecordEvent(PLAYER, ROOM, Event("Navigation", "go.official"));
        await recorder.FlushAsync(Ct);

        var rows = await Rows<ClientEventLogEntity>();
        rows.Count(x => x.PlayerId == PLAYER).Should().Be(4);
        rows.Count(x => x.PlayerId == OTHER).Should().Be(1);
    }

    [Fact]
    public async Task A_full_queue_drops_new_reports_instead_of_waiting_and_off_keeps_nothing()
    {
        _config.Set(new ClientTelemetryConfig { QueueCapacity = 2 });
        var recorder = Recorder();

        for (var i = 0; i < 5; i++)
            recorder.RecordEvent(PLAYER, ROOM, Event("Navigation", "go.official"));

        await recorder.FlushAsync(Ct);
        (await Rows<ClientEventLogEntity>()).Should().HaveCount(2);

        _config.Set(new ClientTelemetryConfig { Enabled = false, QueueCapacity = 2 });
        recorder.RecordEvent(PLAYER, ROOM, Event("Navigation", "go.official"));
        recorder.RecordLoginStep("s9", PLAYER, ClientLoginSteps.SOCKET_OK);
        await recorder.FlushAsync(Ct);

        (await Rows<ClientEventLogEntity>()).Should().HaveCount(2);
        (await Rows<ClientLoginStepEntity>()).Should().BeEmpty();
    }

    [Fact]
    public async Task Login_steps_are_timed_from_the_socket_and_kept_once_each()
    {
        var recorder = Recorder();

        recorder.RecordLoginStep("s1", -1, ClientLoginSteps.SOCKET_OK);
        _time.Advance(TimeSpan.FromMilliseconds(120));
        recorder.RecordLoginStep("s1", -1, ClientLoginSteps.HANDSHAKE_START);
        _time.Advance(TimeSpan.FromMilliseconds(300));
        recorder.RecordLoginStep("s1", PLAYER, ClientLoginSteps.AUTH_OK);
        _time.Advance(TimeSpan.FromMilliseconds(500));
        recorder.RecordLoginStep("s1", PLAYER, ClientLoginSteps.ROOM_ENTER);
        _time.Advance(TimeSpan.FromSeconds(30));
        recorder.RecordLoginStep("s1", PLAYER, ClientLoginSteps.ROOM_ENTER);
        // A session the server never saw open, and a first room long after logging in.
        recorder.RecordLoginStep("unknown", PLAYER, ClientLoginSteps.AUTH_OK);
        recorder.RecordLoginStep("s2", -1, ClientLoginSteps.SOCKET_OK);
        _time.Advance(TimeSpan.FromMinutes(11));
        recorder.RecordLoginStep("s2", OTHER, ClientLoginSteps.ROOM_ENTER);
        await recorder.FlushAsync(Ct);

        var rows = await Rows<ClientLoginStepEntity>();
        rows.Where(x => x.Session == "s1")
            .OrderBy(x => x.MsSinceConnect)
            .Select(x => (x.Step, x.MsSinceConnect, x.PlayerId))
            .Should()
            .Equal(
                (ClientLoginSteps.SOCKET_OK, 0, (int?)null),
                (ClientLoginSteps.HANDSHAKE_START, 120, (int?)null),
                (ClientLoginSteps.AUTH_OK, 420, (int?)PLAYER),
                (ClientLoginSteps.ROOM_ENTER, 920, (int?)PLAYER)
            );
        rows.Should().NotContain(x => x.Session == "unknown");
        rows.Where(x => x.Session == "s2")
            .Select(x => x.Step)
            .Should()
            .Equal(ClientLoginSteps.SOCKET_OK);
    }

    [Fact]
    public async Task The_handshake_and_the_first_room_are_login_steps()
    {
        var recorder = Recorder();
        var harness = new PacketHarness().WithRoomsExisting();
        harness.Resolver.Overrides[typeof(IClientTelemetry)] = recorder;
        harness.Fakes.Handlers["get_SessionKey"] = _ => new SessionKey("socket-1");
        harness.Resolver.Overrides[typeof(IDiffieService)] = harness.Fakes.Create<IDiffieService>();
        harness.Fakes.Handlers["GetSignedPrime"] = _ => "1";
        harness.Fakes.Handlers["GetPendingRoomEntryAsync"] = _ =>
            Task.FromResult(RoomEntrySnapshot.Default);
        // The room service lets the player in; entering itself is not what this is about.
        harness.Resolver.Overrides[typeof(IRoomService)] = harness.Fakes.Create<IRoomService>();
        harness.Fakes.Handlers["CheckRoomEntryAccessAsync"] = _ =>
            Task.FromResult(RoomEntryAccessType.Allowed);
        harness.Fakes.Handlers["GetSignedGenerator"] = _ => "2";

        await harness.SendAsync(
            PacketHarness.Incoming("ClientHelloMessageEvent"),
            PacketHarness.Payload(w =>
                w.String("WIN63-202610201032").String("FLASH").Int(0).Int(0)
            ),
            -1
        );
        _time.Advance(TimeSpan.FromMilliseconds(40));
        await harness.SendAsync(PacketHarness.Incoming("InitDiffieHandshakeMessageEvent"), [], -1);
        _time.Advance(TimeSpan.FromMilliseconds(400));
        await harness.SendAsync(
            PacketHarness.Incoming("OpenFlatConnectionMessageEvent"),
            PacketHarness.Payload(w => w.Int(ROOM).String("").Int(-1)),
            PLAYER
        );
        await recorder.FlushAsync(Ct);

        (await Rows<ClientLoginStepEntity>())
            .Where(x => x.Session == "socket-1")
            .OrderBy(x => x.MsSinceConnect)
            .Select(x => (x.Step, x.MsSinceConnect))
            .Should()
            .Equal(
                (ClientLoginSteps.SOCKET_OK, 0),
                (ClientLoginSteps.HANDSHAKE_START, 40),
                (ClientLoginSteps.ROOM_ENTER, 440)
            );
    }

    [Fact]
    public async Task Maintenance_rolls_finished_days_into_figures_then_expires_old_rows()
    {
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var yesterday = At(today.AddDays(-1));
        var longAgo = At(today.AddDays(-40));

        await AddAsync(
            Perf(yesterday, PLAYER, ROOM, CHROME_WINDOWS, 20, 300_000),
            Perf(yesterday, OTHER, ROOM, CHROME_WINDOWS, 50, 100_000),
            Perf(yesterday, OTHER, null, SAFARI_IPHONE, 33, 200_000),
            Perf(longAgo, PLAYER, null, CHROME_WINDOWS, 16, 100_000),
            Perf(At(today), PLAYER, null, CHROME_WINDOWS, 16, 100_000),
            new ClientLagWarningEntity
            {
                CreatedAt = yesterday,
                PlayerId = PLAYER,
                RoomId = ROOM,
                WarningCount = 3,
            },
            new ClientEventLogEntity
            {
                CreatedAt = yesterday,
                PlayerId = PLAYER,
                RoomId = ROOM,
                Category = ClientTelemetryKinds.CLIENT_PERFORMANCE_CATEGORY,
                Type = "24",
                Action = "fps",
                ExtraString = "Avatars: 1, Objects: 44",
                ExtraInt = ROOM,
            },
            Ev(yesterday, PLAYER, "Navigation", "go.official"),
            Ev(yesterday, PLAYER, "Navigation", "go.official"),
            Ev(yesterday, OTHER, "Navigation", "go.official"),
            new ClientTelemetryDailyEntity
            {
                Day = today.AddDays(-400),
                Kind = ClientTelemetryKinds.DAY,
                Key1 = "",
                Key2 = "",
                Key3 = "",
                Samples = 0,
                Players = 0,
                Sum = 0,
                P50 = 0,
                P95 = 0,
                Max = 0,
            }
        );

        var rolled = await Maintenance().RunAsync(Ct);
        var again = await Maintenance().RunAsync(Ct);

        rolled.Should().Be(40, "every finished day since the oldest raw row, once");
        again.Should().Be(0);

        var figures = await Rows<ClientTelemetryDailyEntity>();
        figures.Should().NotContain(x => x.Day == today || x.Day < today.AddDays(-365));

        var fps = figures.Single(x =>
            x.Day == today.AddDays(-1) && x.Kind == ClientTelemetryKinds.FPS && x.Key1 == "Chrome"
        );
        (fps.Samples, fps.Players, fps.P50, fps.P95).Should().Be((2, 2, 20, 50));
        figures
            .Single(x => x.Day == today.AddDays(-1) && x.Kind == ClientTelemetryKinds.ROOM_FPS)
            .Should()
            .Match<ClientTelemetryDailyEntity>(x =>
                x.Key1 == "77" && x.Samples == 3 && x.P50 == 24
            );
        figures
            .Single(x => x.Day == today.AddDays(-1) && x.Kind == ClientTelemetryKinds.ROOM_LAG)
            .Sum.Should()
            .Be(3);
        figures
            .Single(x => x.Day == today.AddDays(-1) && x.Kind == ClientTelemetryKinds.EVENT)
            .Should()
            .Match<ClientTelemetryDailyEntity>(x =>
                x.Key1 == "Navigation" && x.Samples == 3 && x.Players == 2
            );
        figures
            .Should()
            .ContainSingle(x => x.Day == today.AddDays(-40) && x.Kind == ClientTelemetryKinds.FPS);

        var raw = await Rows<ClientPerformanceLogEntity>();
        raw.Should().HaveCount(4, "the row older than 30 days expired once counted");
        raw.Should().NotContain(x => x.CreatedAt == longAgo);
    }

    private ClientTelemetryRecorder Recorder() =>
        new(_config, _db, _time, NullLogger<ClientTelemetryRecorder>.Instance);

    private ClientTelemetryMaintenance Maintenance() =>
        new(_config, _db, _time, NullLogger<ClientTelemetryMaintenance>.Instance);

    private async Task<List<T>> Rows<T>()
        where T : class
    {
        await using var ctx = await _db.CreateDbContextAsync(Ct);

        return await ctx.Set<T>().AsNoTracking().ToListAsync(Ct);
    }

    private async Task AddAsync(params object[] rows)
    {
        await using var ctx = await _db.CreateDbContextAsync(Ct);
        ctx.AddRange(rows);
        await ctx.SaveChangesAsync(Ct);
    }

    private static DateTime At(DateOnly day) =>
        day.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc);

    private static PerformanceLogMessage Performance(string userAgent, int interval) =>
        new()
        {
            UserAgent = userAgent,
            AverageUpdateInterval = interval,
            MemoryUsage = 100_000,
        };

    private static EventLogMessage Event(string category, string action) =>
        new()
        {
            Category = category,
            Type = "",
            Action = action,
            ExtraString = "",
            ExtraInt = 0,
        };

    private static ClientEventLogEntity Ev(
        DateTime at,
        int player,
        string category,
        string action
    ) =>
        new()
        {
            CreatedAt = at,
            PlayerId = player,
            RoomId = ROOM,
            Category = category,
            Type = "",
            Action = action,
            ExtraString = "",
            ExtraInt = 0,
        };

    private static ClientPerformanceLogEntity Perf(
        DateTime at,
        int player,
        int? room,
        string userAgent,
        int fps,
        int memoryKb
    )
    {
        var (browser, os, device) = ClientUserAgents.Classify(userAgent);

        return new ClientPerformanceLogEntity
        {
            CreatedAt = at,
            PlayerId = player,
            RoomId = room,
            ElapsedSeconds = 60,
            UserAgent = userAgent,
            FlashVersion = "",
            Os = "",
            Browser = "",
            BrowserFamily = browser,
            OsFamily = os,
            DeviceType = device,
            IsDebugger = false,
            MemoryKb = memoryKb,
            GarbageCollections = 0,
            AverageUpdateIntervalMs = 1000 / fps,
            SlowUpdates = 1,
        };
    }
}
