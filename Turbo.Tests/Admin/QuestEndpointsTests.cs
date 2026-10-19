using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Quests;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Quests.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's daily tasks and quests (<c>/api/quests</c>): a daily task the hotel could not
/// give is refused with why unless it is saved off, a saved change has the players online read
/// the tasks again, and taking one away takes the players' tasks of it; quests and campaigns are
/// saved and listed with whether the server counts their type; only staff who may change content
/// change them.
/// </summary>
public sealed class QuestEndpointsTests : IAsyncDisposable
{
    private const int ALICE = 2;

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly int _port;
    private readonly IHostedService _server;

    private Func<string, bool> _holds = _ => true;

    public QuestEndpointsTests()
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
            (IReadOnlyCollection<PlayerId>)[new PlayerId(ALICE)];

        _db.Insert(
            new PlayerEntity
            {
                Id = ALICE,
                Name = "Alice",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
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
    public async Task A_daily_task_the_hotel_could_not_give_is_refused_unless_saved_off()
    {
        await _server.StartAsync(Ct);

        var refused = await SendAsync(
            HttpMethod.Post,
            "/api/quests/daily-tasks",
            DailyTask("find_furni", target: "", enabled: true)
        );

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(refused))
            .GetProperty("message")
            .GetString()
            .Should()
            .Contain("furni to find");

        var kept = await SendAsync(
            HttpMethod.Post,
            "/api/quests/daily-tasks",
            DailyTask("find_furni", target: "", enabled: false)
        );

        kept.StatusCode.Should().Be(HttpStatusCode.OK);
        var listed = (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/quests/daily-tasks")))
            .GetProperty("tasks")
            .EnumerateArray()
            .Single();
        listed.GetProperty("enabled").GetBoolean().Should().BeFalse();
        listed.GetProperty("problem").GetString().Should().Contain("furni to find");
    }

    [Fact]
    public async Task A_daily_task_saved_has_the_players_online_read_the_tasks_and_removing_it_takes_theirs()
    {
        await _server.StartAsync(Ct);

        var id = (
            await JsonAsync(
                await SendAsync(
                    HttpMethod.Post,
                    "/api/quests/daily-tasks",
                    DailyTask("explore", target: "", enabled: true)
                )
            )
        )
            .GetProperty("id")
            .GetInt32();

        _fakes.Log.Of("ReloadAsync").Select(x => Convert.ToInt64(x.Key)).Should().Equal(ALICE);

        _db.Insert(
            new PlayerDailyTaskEntity
            {
                Id = 9,
                PlayerEntityId = ALICE,
                DefinitionEntityId = id,
                DayStartsAt = DateTime.UtcNow.Date,
            }
        );

        var listed = (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/quests/daily-tasks")))
            .GetProperty("tasks")
            .EnumerateArray()
            .Single();
        listed.GetProperty("given").GetInt32().Should().Be(1);
        listed.GetProperty("problem").ValueKind.Should().Be(JsonValueKind.Null);

        (await SendAsync(HttpMethod.Delete, $"/api/quests/daily-tasks/{id}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);

        await using var db = _db.CreateDbContext();
        db.PlayerDailyTasks.Should().BeEmpty();
        _fakes.Log.Of("ReloadAsync").Should().HaveCount(2);
    }

    [Fact]
    public async Task A_quest_is_saved_in_its_campaign_and_says_whether_its_type_is_counted()
    {
        await _server.StartAsync(Ct);

        var campaign = (
            await JsonAsync(
                await SendAsync(
                    HttpMethod.Post,
                    "/api/quests/campaigns",
                    new
                    {
                        code = "autumn",
                        sortOrder = 1,
                        enabled = true,
                        endsAtUtc = (DateTime?)null,
                    }
                )
            )
        ).GetProperty("id").GetInt32();

        (
            await SendAsync(
                HttpMethod.Post,
                "/api/quests/quests",
                Quest(campaign, "wear", QuestTypes.WEAR_BADGE)
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        (
            await SendAsync(
                HttpMethod.Post,
                "/api/quests/quests",
                Quest(campaign, "dots", "CONNECT_DOTS_1")
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        (
            await SendAsync(
                HttpMethod.Post,
                "/api/quests/quests",
                Quest(campaign, "wear", QuestTypes.PET_EAT)
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);

        var listed = (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/quests")))
            .GetProperty("campaigns")
            .EnumerateArray()
            .Single();
        listed.GetProperty("code").GetString().Should().Be("autumn");
        listed
            .GetProperty("quests")
            .EnumerateArray()
            .Select(x =>
                (
                    x.GetProperty("localizationCode").GetString(),
                    x.GetProperty("counted").GetBoolean()
                )
            )
            .Should()
            .Equal(("wear", true), ("dots", false));
    }

    [Fact]
    public async Task Staff_who_may_only_see_content_list_tasks_and_quests_but_change_none()
    {
        _holds = node => node != PermissionNodes.Content.MANAGE;
        await _server.StartAsync(Ct);

        (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/quests/daily-tasks")))
            .GetProperty("canManage")
            .GetBoolean()
            .Should()
            .BeFalse();
        (
            await SendAsync(
                HttpMethod.Post,
                "/api/quests/daily-tasks",
                DailyTask("explore", "", true)
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        (
            await SendAsync(
                HttpMethod.Post,
                "/api/quests/campaigns",
                new
                {
                    code = "x",
                    sortOrder = 0,
                    enabled = true,
                }
            )
        ).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _holds = node => node != PermissionNodes.Admin.CONTENT_VIEW;

        (await SendAsync(HttpMethod.Get, "/api/quests"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    private static object DailyTask(string type, string target, bool enabled) =>
        new
        {
            code = $"task_{type}",
            questType = type,
            target,
            requiredRepeats = 3,
            isBonus = false,
            enabled,
            rewardKind = "points",
            rewardTypeId = "0",
            rewardAmount = 10,
        };

    private static object Quest(int campaign, string code, string type) =>
        new
        {
            campaignId = campaign,
            localizationCode = code,
            type,
            totalSteps = 1,
            activityPointType = 0,
            rewardAmount = 50,
            sortOrder = 0,
            easy = true,
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
