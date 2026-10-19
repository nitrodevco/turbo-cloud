using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Entities.Players;
using Turbo.Operations;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Events;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's reports (<c>/api/reports</c>): staff with the mod tool's node list the calls for
/// help and read one with its chat lines; closing one there is the mod tool's own close, so the
/// report is closed and its sender told, and a report another moderator holds stays theirs.
/// </summary>
public sealed class ReportEndpointsTests : IAsyncDisposable
{
    /// <summary>The staff member signed in to the panel.</summary>
    private const int STAFF = 1;
    private const int REPORTER = 2;
    private const int REPORTED = 3;
    private const int OTHER_MODERATOR = 4;

    private readonly Fakes _fakes = new();
    private readonly TestEventBus _events = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly int _port;
    private readonly IHostedService _server;

    /// <summary>The nodes the staff member holds.</summary>
    private Func<string, bool> _holds = _ => true;

    public ReportEndpointsTests()
    {
        _port = TestPorts.Free();

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = STAFF,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(_holds((string)call.Args[0]!));
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();
        _fakes.Handlers[nameof(ISessionGateway.GetOnlinePlayerIds)] = _ =>
            (IReadOnlyCollection<PlayerId>)[];

        // The hotel's own call for help queue, as the client's mod tool reaches it.
        _fakes.Instances[(typeof(IModerationIssueService), null)] = new ModerationIssueService(
            _db,
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<ISessionGateway>(),
            TimeProvider.System,
            Options.Create(new OperationsConfig()),
            _fakes.Create<IModerationToolService>(),
            _fakes.Create<IHotelTextProvider>(),
            _events.System
        );
        _events.Record<CallForHelpChangedEvent>();

        _db.Insert(Player(STAFF, "Staff"));
        _db.Insert(Player(REPORTER, "Sender"));
        _db.Insert(Player(REPORTED, "Rude"));
        _db.Insert(Player(OTHER_MODERATOR, "Moderator"));
        _db.Insert(
            new CfhTopicEntity
            {
                Id = 11,
                Category = "harassment",
                Name = "bullying",
                Consequence = "mute",
            }
        );

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
    public async Task An_open_report_is_listed_and_read_with_its_lines_and_closing_it_tells_its_sender()
    {
        Report(1, REPORTED, "They keep calling me names");
        _db.Insert(Line(1, 0, REPORTED, "you are awful"));
        _db.Insert(Line(1, 1, REPORTER, "please stop"));
        await _server.StartAsync(Ct);

        var list = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/reports"));
        list.GetProperty("open").GetInt32().Should().Be(1);
        var listed = list.GetProperty("entries").EnumerateArray().Single();
        listed.GetProperty("state").GetString().Should().Be("open");
        listed.GetProperty("reporterName").GetString().Should().Be("Sender");
        listed.GetProperty("reportedName").GetString().Should().Be("Rude");
        listed.GetProperty("topic").GetString().Should().Be("bullying");

        var detail = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/reports/1"));
        detail
            .GetProperty("lines")
            .EnumerateArray()
            .Select(x =>
                (x.GetProperty("text").GetString(), x.GetProperty("reported").GetBoolean())
            )
            .Should()
            .Equal(("you are awful", true), ("please stop", false));

        var closed = await SendAsync(HttpMethod.Post, "/api/reports/1/close", Close("resolved"));

        closed.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = (await JsonAsync(closed)).GetProperty("report");
        report.GetProperty("state").GetString().Should().Be("closed");
        report.GetProperty("closeReason").GetString().Should().Be("resolved");
        report.GetProperty("pickerId").GetInt32().Should().Be(STAFF);
        _fakes
            .Log.Of("TrySendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<IssueCloseNotificationMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.CloseReason.Should()
            .Be(CfhCloseReasonType.Resolved);
        // The panel's own stream hears of it, as it does of a close in the hotel.
        await _events.WhenPublishedAsync<CallForHelpChangedEvent>(2);
        _events.Of<CallForHelpChangedEvent>().Select(x => x.IssueIds.Single()).Should().Equal(1, 1);
        (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/reports")))
            .GetProperty("entries")
            .GetArrayLength()
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task A_report_another_moderator_holds_is_not_closed_and_stays_theirs()
    {
        Report(1, REPORTED, "Spam", picker: OTHER_MODERATOR);
        await _server.StartAsync(Ct);

        var refused = await SendAsync(HttpMethod.Post, "/api/reports/1/close", Close("useless"));

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await JsonAsync(refused)).GetProperty("message").GetString().Should().Contain("Moderator");
        var report = (
            await JsonAsync(await SendAsync(HttpMethod.Get, "/api/reports/1"))
        ).GetProperty("report");
        report.GetProperty("state").GetString().Should().Be("picked");
        report.GetProperty("pickerId").GetInt32().Should().Be(OTHER_MODERATOR);
    }

    [Fact]
    public async Task Closing_with_the_related_reports_closes_all_or_none_of_them()
    {
        Report(1, REPORTED, "Scamming");
        Report(2, REPORTED, "Scamming me too");
        Report(3, REPORTED, "And me", picker: OTHER_MODERATOR);
        await _server.StartAsync(Ct);

        // One of them is another moderator's: nothing is closed, and the staff member's picks go back.
        (
            await SendAsync(
                HttpMethod.Post,
                "/api/reports/1/close",
                Close("resolved", related: true)
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.Conflict);
        var states = await StatesAsync();
        states.Should().Equal(("open", 0), ("open", 0), ("picked", OTHER_MODERATOR));

        // The other moderator lets theirs go: now the three close together.
        await using (var db = _db.CreateDbContext())
        {
            (await db.CfhReports.FindAsync([3], Ct))!.PickerEntityId = null;
            await db.SaveChangesAsync(Ct);
        }

        (await SendAsync(HttpMethod.Post, "/api/reports/1/close", Close("resolved", related: true)))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        (await StatesAsync()).Should().AllSatisfy(x => x.State.Should().Be("closed"));
    }

    [Fact]
    public async Task Staff_without_the_mod_tool_can_not_see_reports()
    {
        _holds = node => node != PermissionNodes.Moderation.TOOL;
        Report(1, REPORTED, "Hello");
        await _server.StartAsync(Ct);

        (await SendAsync(HttpMethod.Get, "/api/reports"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, "/api/reports/1/close", Close("resolved")))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/me")))
            .GetProperty("canHandleReports")
            .GetBoolean()
            .Should()
            .BeFalse();
    }

    private async Task<List<(string? State, int Picker)>> StatesAsync() =>
        [
            .. (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/reports?state=all")))
                .GetProperty("entries")
                .EnumerateArray()
                .OrderBy(x => x.GetProperty("id").GetInt32())
                .Select(x =>
                    (
                        x.GetProperty("state").GetString(),
                        x.GetProperty("pickerId").ValueKind == JsonValueKind.Number
                            ? x.GetProperty("pickerId").GetInt32()
                            : 0
                    )
                ),
        ];

    private void Report(int id, int reported, string message, int? picker = null) =>
        _db.Insert(
            new CfhReportEntity
            {
                Id = id,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10 + id),
                ReporterEntityId = REPORTER,
                ReportedEntityId = reported,
                TopicId = 11,
                Message = message,
                PickerEntityId = picker,
            }
        );

    private static CfhReportChatLineEntity Line(
        int report,
        int position,
        int player,
        string text
    ) =>
        new()
        {
            Id = report * 100 + position,
            ReportEntityId = report,
            Position = position,
            PlayerEntityId = player,
            Text = text,
        };

    private static object Close(string reason, bool related = false) =>
        new
        {
            reason,
            sanction = false,
            withRelated = related,
        };

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body = null
    )
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}{path}");

        request.Headers.Authorization = new("Bearer", "a-session");

        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
