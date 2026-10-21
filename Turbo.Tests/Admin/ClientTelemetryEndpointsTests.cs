using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Tracking;
using Turbo.Database.Tracking;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Tracking;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's Client health and Usage pages (<c>/api/telemetry/*</c>): staff with
/// <c>admin.telemetry.view</c> read frame rate, memory and latency percentiles by browser, the
/// rooms with the most chat lag, the login steps and the events clients log, from the rolled-up
/// days and today's raw rows alike, and drill down to one player's or room's reports.
/// </summary>
public sealed class ClientTelemetryEndpointsTests : IAsyncDisposable
{
    private const int ALICE = 2;
    private const int BOB = 3;
    private const int ROOM = 40;
    private const string CHROME =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";
    private const string FIREFOX =
        "Mozilla/5.0 (X11; Linux x86_64; rv:144.0) Gecko/20100101 Firefox/144.0";

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly int _port;
    private readonly IHostedService _server;
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow);

    private Func<string, bool> _holds = _ => true;

    public ClientTelemetryEndpointsTests()
    {
        _port = TestPorts.Free();

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(_holds((string)call.Args[0]!));
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();
        _fakes.Instances[(typeof(IOptionsMonitor<ClientTelemetryConfig>), null)] =
            new TestOptionsMonitor<ClientTelemetryConfig>(new ClientTelemetryConfig());

        _db.Insert(Player(ALICE, "Alice"));
        _db.Insert(Player(BOB, "Bob"));

        (_server, _, _) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{_port}",
            _fakes,
            NullLoggerFactory.Instance
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _server.StopAsync(CancellationToken.None);
        _db.Dispose();
    }

    [Fact]
    public async Task Health_gives_percentiles_by_browser_the_laggiest_rooms_and_the_login_steps()
    {
        var yesterday = At(_today.AddDays(-1));
        var now = At(_today);
        await AddAsync(
            Perf(yesterday, ALICE, ROOM, CHROME, 20),
            Perf(yesterday, BOB, ROOM, CHROME, 40),
            Perf(now, BOB, null, FIREFOX, 60),
            new ClientLatencyReportEntity
            {
                CreatedAt = now,
                PlayerId = BOB,
                RoomId = ROOM,
                AverageMs = 120,
                ValidAverageMs = 110,
                PingCount = 3,
                BrowserFamily = "Firefox",
                OsFamily = "Linux",
                DeviceType = "desktop",
            },
            new ClientLagWarningEntity
            {
                CreatedAt = now,
                PlayerId = ALICE,
                RoomId = ROOM,
                WarningCount = 5,
            },
            Step(now, "a", ClientLoginSteps.SOCKET_OK, 0),
            Step(now, "a", ClientLoginSteps.AUTH_OK, 400),
            Step(now, "b", ClientLoginSteps.SOCKET_OK, 0)
        );
        // Yesterday is rolled up, as the maintenance does every hour; today is made from raw rows.
        await new ClientTelemetryMaintenance(
            new TestOptionsMonitor<ClientTelemetryConfig>(new ClientTelemetryConfig()),
            _db,
            TimeProvider.System,
            NullLogger<ClientTelemetryMaintenance>.Instance
        ).RunAsync(Ct);
        await _server.StartAsync(Ct);

        var health = await GetAsync("/api/telemetry/health");

        var devices = health.GetProperty("devices").EnumerateArray().ToList();
        var chrome = devices.Single(x => x.GetProperty("browser").GetString() == "Chrome");
        chrome.GetProperty("reports").GetInt32().Should().Be(2);
        chrome.GetProperty("fpsP50").GetInt32().Should().Be(20);
        chrome.GetProperty("fpsP95").GetInt32().Should().Be(40);
        var firefox = devices.Single(x => x.GetProperty("browser").GetString() == "Firefox");
        firefox.GetProperty("latencyP50Ms").GetInt32().Should().Be(120);

        var room = health.GetProperty("rooms")[0];
        room.GetProperty("roomId").GetInt32().Should().Be(ROOM);
        room.GetProperty("lagWarnings").GetInt64().Should().Be(5);

        health
            .GetProperty("login")
            .EnumerateArray()
            .Select(x => (x.GetProperty("step").GetString(), x.GetProperty("sessions").GetInt32()))
            .Should()
            .StartWith((ClientLoginSteps.SOCKET_OK, 2))
            .And.Contain((ClientLoginSteps.AUTH_OK, 1));
        health.GetProperty("days").GetArrayLength().Should().Be(7);
        health
            .GetProperty("filters")
            .GetProperty("browsers")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Should()
            .Equal("Chrome", "Firefox");

        var onlyFirefox = await GetAsync("/api/telemetry/health?browser=Firefox");
        onlyFirefox.GetProperty("devices").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Usage_counts_events_by_kind_without_the_frame_rate_events_and_funnels_players()
    {
        var now = At(_today);
        await AddAsync(
            Ev(now, ALICE, "Navigation", "go.official"),
            Ev(now, ALICE, "Navigation", "go.official"),
            Ev(now, BOB, "Navigation", "go.official"),
            Ev(now, ALICE, "Catalog", "open"),
            Ev(now, ALICE, ClientTelemetryKinds.CLIENT_PERFORMANCE_CATEGORY, "fps")
        );
        await _server.StartAsync(Ct);

        var usage = await GetAsync("/api/telemetry/usage");

        var top = usage.GetProperty("top")[0];
        (top.GetProperty("category").GetString(), top.GetProperty("count").GetInt64())
            .Should()
            .Be(("Navigation", 3L));
        usage
            .GetProperty("categories")
            .EnumerateArray()
            .Select(x => x.GetProperty("category").GetString())
            .Should()
            .Equal("Navigation", "Catalog");
        usage
            .GetProperty("days")
            .EnumerateArray()
            .Last()
            .GetProperty("events")
            .GetInt64()
            .Should()
            .Be(4);

        var funnel = await GetArrayAsync(
            "/api/telemetry/funnel?step=Navigation||go.official&step=Catalog||open"
        );
        funnel.Select(x => x.GetProperty("players").GetInt32()).Should().Equal(2, 1);
    }

    [Fact]
    public async Task A_players_and_a_rooms_reports_are_listed_and_only_for_staff_with_the_node()
    {
        var now = At(_today);
        await AddAsync(
            Perf(now, ALICE, ROOM, CHROME, 25),
            Ev(now, ALICE, "Navigation", "go.official"),
            new ClientLagWarningEntity
            {
                CreatedAt = now,
                PlayerId = BOB,
                RoomId = ROOM,
                WarningCount = 2,
            }
        );
        await _server.StartAsync(Ct);

        var alice = await GetAsync($"/api/telemetry/players/{ALICE}");
        alice.GetProperty("playerName").GetString().Should().Be("Alice");
        var perf = alice.GetProperty("performance")[0];
        perf.GetProperty("fps").GetInt32().Should().Be(25);
        perf.GetProperty("roomId").GetInt32().Should().Be(ROOM);
        alice.GetProperty("events")[0].GetProperty("action").GetString().Should().Be("go.official");

        var room = await GetAsync($"/api/telemetry/rooms/{ROOM}");
        room.GetProperty("lag")[0].GetProperty("playerName").GetString().Should().Be("Bob");
        room.GetProperty("days")
            .EnumerateArray()
            .Last()
            .GetProperty("lagWarnings")
            .GetInt64()
            .Should()
            .Be(2);

        _holds = node => node != PermissionNodes.Admin.TELEMETRY_VIEW;
        (await SendAsync("/api/telemetry/health")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync($"/api/telemetry/players/{ALICE}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    private async Task AddAsync(params object[] rows)
    {
        await using var ctx = await _db.CreateDbContextAsync(Ct);
        ctx.AddRange(rows);
        await ctx.SaveChangesAsync(Ct);
    }

    private Task<HttpResponseMessage> SendAsync(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{_port}{path}");
        request.Headers.Authorization = new("Bearer", "a-session");

        return _client.SendAsync(request, Ct);
    }

    private async Task<JsonElement> GetAsync(string path)
    {
        var response = await SendAsync(path);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
    }

    private async Task<List<JsonElement>> GetArrayAsync(string path) =>
        [.. (await GetAsync(path)).EnumerateArray()];

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };

    private static DateTime At(DateOnly day) =>
        day.ToDateTime(new TimeOnly(0, 30), DateTimeKind.Utc);

    private static ClientLoginStepEntity Step(DateTime at, string session, string step, int ms) =>
        new()
        {
            CreatedAt = at,
            Session = session,
            Step = step,
            MsSinceConnect = ms,
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
            Type = category == ClientTelemetryKinds.CLIENT_PERFORMANCE_CATEGORY ? "30" : "",
            Action = action,
            ExtraString = "",
            ExtraInt = 0,
        };

    private static ClientPerformanceLogEntity Perf(
        DateTime at,
        int player,
        int? room,
        string ua,
        int fps
    )
    {
        var (browser, os, device) = ClientUserAgents.Classify(ua);

        return new ClientPerformanceLogEntity
        {
            CreatedAt = at,
            PlayerId = player,
            RoomId = room,
            ElapsedSeconds = 60,
            UserAgent = ua,
            FlashVersion = "",
            Os = "",
            Browser = "",
            BrowserFamily = browser,
            OsFamily = os,
            DeviceType = device,
            IsDebugger = false,
            MemoryKb = 204_800,
            GarbageCollections = 0,
            AverageUpdateIntervalMs = 1000 / fps,
            SlowUpdates = 0,
        };
    }
}
