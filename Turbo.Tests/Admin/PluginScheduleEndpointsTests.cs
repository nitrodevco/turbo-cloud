using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Scheduling.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The Plugins page's Schedule tab (<c>/api/plugins/schedule</c>, docs/plugin-scheduler.md):
/// listed with <c>admin.plugins.view</c>; Run now needs <c>admin.plugins.manage</c>, and every
/// press, refused ones too, is in the command log.
/// </summary>
public sealed class PluginScheduleEndpointsTests : IAsyncDisposable
{
    private const string KEY = "rares.WeeklyRotation";

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly List<string> _runNow = [];
    private readonly int _port = TestPorts.Free();
    private readonly IHostedService _server;

    private Func<string, bool> _holds = _ => true;
    private ScheduledTaskRunNow _answer = ScheduledTaskRunNow.Started;

    public PluginScheduleEndpointsTests()
    {
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
        // The scheduler grain: one task, and what run now answers.
        _fakes.Handlers["ListAsync"] = _ =>
            Task.FromResult<ImmutableArray<ScheduledTaskSnapshot>>([
                new ScheduledTaskSnapshot
                {
                    Key = KEY,
                    Plugin = "rares",
                    Schedule = "0 18 * * MON",
                    Description = "Mondays 18:00",
                    LastRun = new ScheduledTaskResult(
                        ScheduledTaskOutcome.Failed,
                        DateTimeOffset.UtcNow,
                        12,
                        "boom",
                        false
                    ),
                    Servers = 1,
                },
            ]);
        _fakes.Handlers["RunNowAsync"] = call =>
        {
            _runNow.Add((string)call.Args[0]!);

            return Task.FromResult(_answer);
        };

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
    public async Task Staff_who_cannot_manage_see_the_schedule_but_cannot_run_a_task()
    {
        _holds = node => node != PermissionNodes.Admin.PLUGINS_MANAGE;
        await _server.StartAsync(Ct);

        var list = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/plugins/schedule"));
        list.GetProperty("canManage").GetBoolean().Should().BeFalse();
        var task = list.GetProperty("tasks").EnumerateArray().Single();
        task.GetProperty("key").GetString().Should().Be(KEY);
        task.GetProperty("description").GetString().Should().Be("Mondays 18:00");
        task.GetProperty("lastOutcome").GetString().Should().Be("Failed");

        (await SendAsync(HttpMethod.Post, $"/api/plugins/schedule/{KEY}/run"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        _runNow.Should().BeEmpty();
        Log().Should().Equal([($"run {KEY}", "refused")]);
    }

    [Fact]
    public async Task Run_now_starts_the_task_and_says_why_when_it_cant()
    {
        await _server.StartAsync(Ct);

        (await SendAsync(HttpMethod.Post, $"/api/plugins/schedule/{KEY}/run"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        _answer = ScheduledTaskRunNow.AlreadyRunning;
        (await SendAsync(HttpMethod.Post, $"/api/plugins/schedule/{KEY}/run"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Conflict);
        _answer = ScheduledTaskRunNow.Unknown;
        (await SendAsync(HttpMethod.Post, "/api/plugins/schedule/rares.Gone/run"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);

        _runNow.Should().Equal(KEY, KEY, "rares.Gone");
        Log()
            .Should()
            .Equal([
                ($"run {KEY}", "completed"),
                ($"run {KEY}", "failed"),
                ("run rares.Gone", "failed"),
            ]);
    }

    private List<(string Arguments, string Outcome)> Log()
    {
        using var db = _db.CreateDbContext();

        db.CommandLogs.Should().OnlyContain(x => x.Command == "plugin" && x.Source == "panel");

        return
        [
            .. db.CommandLogs.OrderBy(x => x.Id).ToList().Select(x => (x.Arguments, x.Outcome)),
        ];
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}{path}");

        request.Headers.Authorization = new("Bearer", "a-session");

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
