using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Operations;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Restarting the server from the panel (<c>/api/server/restart</c>) over the real availability
/// service: only staff with <c>admin.server.restart</c> may start or call off a restart, which
/// counts down as a shutdown does, warns the hotel, and ends the process with the restart exit
/// code; it does not overrule a shutdown. Every request, refused ones too, is in the command log.
/// </summary>
public sealed class ServerRestartEndpointsTests : IAsyncDisposable
{
    private static readonly DateTime START = new(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc);

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly ManualTimeProvider _clock = new(START);
    private readonly HotelAvailabilityService _availability;
    private readonly int _port;
    private readonly IHostedService _server;

    /// <summary>The nodes the staff member holds.</summary>
    private Func<string, bool> _holds = _ => true;

    public ServerRestartEndpointsTests()
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
        _fakes.Handlers["GetOnlinePlayerIds"] = _ =>
            (IReadOnlyCollection<PlayerId>)new List<PlayerId> { new(20) };

        _availability = new HotelAvailabilityService(
            new TestOptionsMonitor<OperationsConfig>(new OperationsConfig()),
            _clock,
            _fakes.Create<ISessionGateway>(),
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<IHotelTextProvider>(),
            _fakes.Create<IHostApplicationLifetime>(),
            new CapturingLogger<IHotelAvailability>()
        );

        // The endpoints reach it through the interface the host registers.
        _fakes.Handlers["get_Current"] = _ => _availability.Current;
        _fakes.Handlers["get_MaxCountdown"] = _ => _availability.MaxCountdown;
        _fakes.Handlers["get_RestartExitCode"] = _ => _availability.RestartExitCode;
        _fakes.Handlers["ScheduleRestart"] = call =>
            _availability.ScheduleRestart((TimeSpan)call.Args[0]!, (string)call.Args[1]!);
        _fakes.Handlers["ScheduleShutdown"] = call =>
        {
            _availability.ScheduleShutdown((TimeSpan)call.Args[0]!, (string)call.Args[1]!);

            return null;
        };
        _fakes.Handlers["Cancel"] = _ => _availability.Cancel();

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
    public async Task Staff_without_the_restart_node_are_refused_and_the_refusal_is_logged()
    {
        _holds = node => node == PermissionNodes.Admin.PANEL;
        await _server.StartAsync(Ct);

        var status = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/server/restart"));

        status.GetProperty("canRestart").GetBoolean().Should().BeFalse();
        (await SendAsync(HttpMethod.Post, "/api/server/restart", new { minutes = 1 }))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Delete, "/api/server/restart"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        _availability.Current.Should().Be(HotelAvailabilitySnapshot.Open);
        Log().Should().Equal(("1", "refused"), ("cancel", "refused"));
    }

    [Fact]
    public async Task A_restart_counts_down_warns_the_hotel_and_ends_with_the_restart_exit_code()
    {
        await _server.StartAsync(Ct);

        var response = await SendAsync(
            HttpMethod.Post,
            "/api/server/restart",
            new { minutes = 2, message = "Applying settings" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await JsonAsync(response);

        status.GetProperty("scheduled").GetBoolean().Should().BeTrue();
        status.GetProperty("reason").GetString().Should().Be("Applying settings");
        status.GetProperty("exitCode").GetInt32().Should().Be(75);
        status.GetProperty("atUtc").GetDateTime().Should().Be(START.AddMinutes(2));

        await _availability.TickAsync(Ct);

        _fakes
            .Log.Of("SendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<InfoHotelClosingMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.MinutesUntilClosing.Should()
            .Be(2);

        _clock.Advance(TimeSpan.FromMinutes(2));
        await _availability.TickAsync(Ct);

        _fakes.Log.Of("DisconnectPlayerAsync").Should().ContainSingle();
        _fakes.Log.Of("StopApplication").Should().ContainSingle();
        _availability.ExitCode.Should().Be(75);
        (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/server/restart")))
            .GetProperty("restarting")
            .GetBoolean()
            .Should()
            .BeTrue();
        Log().Should().Equal(("2 Applying settings", "completed"));
    }

    [Fact]
    public async Task A_restart_can_be_called_off_but_never_calls_off_a_shutdown()
    {
        await _server.StartAsync(Ct);

        await SendAsync(HttpMethod.Post, "/api/server/restart", new { minutes = 5 });
        (await SendAsync(HttpMethod.Delete, "/api/server/restart"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        _availability.Current.Should().Be(HotelAvailabilitySnapshot.Open);

        _availability.ScheduleShutdown(TimeSpan.FromMinutes(5), string.Empty);

        (await SendAsync(HttpMethod.Post, "/api/server/restart", new { minutes = 1 }))
            .StatusCode.Should()
            .Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Delete, "/api/server/restart"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Conflict);
        _availability.Current.Phase.Should().Be(HotelAvailabilityPhase.ShutdownScheduled);
        _availability.Current.Restart.Should().BeFalse();

        Log()
            .Should()
            .Equal(
                ("5", "completed"),
                ("cancel", "completed"),
                ("1", "failed"),
                ("cancel", "failed")
            );
    }

    [Fact]
    public async Task A_countdown_out_of_range_is_refused()
    {
        await _server.StartAsync(Ct);

        (await SendAsync(HttpMethod.Post, "/api/server/restart", new { minutes = 99999 }))
            .StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Post, "/api/server/restart", new { message = "soon" }))
            .StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);

        _availability.Current.Should().Be(HotelAvailabilitySnapshot.Open);
        Log().Should().Equal(("99999", "bind_failed"), ("soon", "bind_failed"));
    }

    private (string Arguments, string Outcome)[] Log()
    {
        using var db = _db.CreateDbContext();

        db.CommandLogs.Should().OnlyContain(x => x.Command == "restart" && x.Source == "panel");

        return
        [
            .. db.CommandLogs.OrderBy(x => x.Id).ToList().Select(x => (x.Arguments, x.Outcome)),
        ];
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body = null
    )
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}{path}");

        if (body is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"
            );

        request.Headers.Authorization = new("Bearer", "a-session");

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
