using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Polls;
using Turbo.Rooms.Grains.Polls;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's room polls (<c>/api/polls</c>): a poll saved there is offered in its room and
/// answered as any poll is, and the panel counts what players answered; saving again keeps the
/// answers of the questions kept; a poll players couldn't answer is refused with why; only staff
/// who may change content change polls.
/// </summary>
public sealed class PollEndpointsTests : IAsyncDisposable
{
    private const int VOTER = 1;
    private const int ROOM_ID = 10;

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly int _port;
    private readonly IHostedService _server;

    private Func<string, bool> _holds = _ => true;

    public PollEndpointsTests()
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

        _db.Insert(
            new PlayerEntity
            {
                Id = VOTER,
                Name = "voter",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "poll-model",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );
        _db.Insert(
            new RoomEntity
            {
                Id = ROOM_ID,
                Name = "poll-room",
                PlayerEntityId = VOTER,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Open,
                UsersNow = 0,
                PlayersMax = 25,
                WallHeight = -1,
                HideWalls = false,
                ThicknessWall = RoomThicknessType.Normal,
                ThicknessFloor = RoomThicknessType.Normal,
                AllowBlocking = false,
                AllowPets = true,
                AllowPetsEat = true,
                TradeType = RoomTradeModeType.Disabled,
                MuteType = ModSettingType.Owner,
                KickType = ModSettingType.Owner,
                BanType = ModSettingType.Owner,
                ChatFloodType = ChatFloodSensitivityType.Minimal,
                PlayerEntity = null!,
                RoomModelEntity = null!,
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
    public async Task A_poll_saved_is_offered_in_its_room_and_its_answers_are_counted()
    {
        await _server.StartAsync(Ct);

        var id = await SaveAsync(null, Poll(Choice(null, "Like it?"), Text(null, "Why?")));
        var detail = await GetAsync($"/api/polls/{id}");
        var choiceQuestion = detail.GetProperty("questions")[0].GetProperty("id").GetInt32();
        var textQuestion = detail.GetProperty("questions")[1].GetProperty("id").GetInt32();
        var grain = Grain();

        await grain.OfferAsync(new RoomId(ROOM_ID), Ct);
        _fakes.Sent<PollOfferEventMessageComposer>().Single().Headline.Should().Be("Tell us");
        await grain.StartAsync(new RoomId(ROOM_ID), id, Ct);
        (await grain.AnswerAsync(id, choiceQuestion, ["yes"], Ct)).Should().BeTrue();
        (await grain.AnswerAsync(id, textQuestion, ["it is fun"], Ct)).Should().BeTrue();

        var results = (await GetAsync($"/api/polls/{id}")).GetProperty("results");
        results[0]
            .GetProperty("choices")
            .EnumerateArray()
            .Select(x => (x.GetProperty("value").GetString(), x.GetProperty("count").GetInt32()))
            .Should()
            .Equal(("yes", 1), ("no", 0));
        var text = results[1].GetProperty("texts").EnumerateArray().Single();
        text.GetProperty("text").GetString().Should().Be("it is fun");
        text.GetProperty("playerName").GetString().Should().Be("voter");
        (await GetAsync("/api/polls"))
            .GetProperty("polls")[0]
            .GetProperty("finished")
            .GetInt32()
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task Saving_again_keeps_the_answers_of_the_questions_kept()
    {
        await _server.StartAsync(Ct);

        var id = await SaveAsync(null, Poll(Choice(null, "Like it?"), Text(null, "Why?")));
        var questions = (await GetAsync($"/api/polls/{id}")).GetProperty("questions");
        var kept = questions[0].GetProperty("id").GetInt32();
        var dropped = questions[1].GetProperty("id").GetInt32();
        var grain = Grain();

        await grain.StartAsync(new RoomId(ROOM_ID), id, Ct);
        await grain.AnswerAsync(id, kept, ["no"], Ct);
        await grain.AnswerAsync(id, dropped, ["meh"], Ct);

        (await SaveAsync(id, Poll(Choice(kept, "Do you like it?")))).Should().Be(id);

        var detail = await GetAsync($"/api/polls/{id}");
        detail
            .GetProperty("questions")
            .EnumerateArray()
            .Select(x => x.GetProperty("text").GetString())
            .Should()
            .Equal("Do you like it?");
        detail.GetProperty("results")[0].GetProperty("answered").GetInt32().Should().Be(1);

        await using var db = await _db.CreateDbContextAsync(Ct);
        db.RoomPollAnswers.Select(x => x.QuestionId).Should().Equal(kept);
    }

    [Fact]
    public async Task A_poll_players_could_not_answer_is_refused_with_why()
    {
        await _server.StartAsync(Ct);

        (
            await MessageAsync(
                Poll(
                    new
                    {
                        id = (int?)null,
                        parent = (int?)null,
                        sortOrder = 0,
                        type = "single",
                        text = "One way?",
                        category = 0,
                        answerType = 0,
                        choices = new[]
                        {
                            new
                            {
                                id = (int?)null,
                                value = "yes",
                                text = "Yes",
                                choiceType = 0,
                            },
                        },
                    }
                )
            )
        ).Should().Contain("two choices");
        (
            await MessageAsync(
                Poll(
                    Choice(null, "First"),
                    new
                    {
                        id = (int?)null,
                        parent = (int?)0,
                        sortOrder = 1,
                        type = "line",
                        text = "Then?",
                        category = 0,
                        answerType = 0,
                        choices = Array.Empty<object>(),
                    }
                )
            )
        ).Should().Contain("score poll");
        (await MessageAsync(Poll(Choice(null, "Here?")) with { roomId = 99 }))
            .Should()
            .Contain("no room 99");
    }

    [Fact]
    public async Task Staff_who_may_only_see_content_read_polls_but_change_none()
    {
        _holds = node => node != PermissionNodes.Content.MANAGE;
        await _server.StartAsync(Ct);

        (await GetAsync("/api/polls")).GetProperty("canManage").GetBoolean().Should().BeFalse();
        (await SendAsync(HttpMethod.Post, "/api/polls", Poll(Choice(null, "Like it?"))))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    private IPlayerRoomPollGrain Grain() =>
        (IPlayerRoomPollGrain)GrainHarness.Create<PlayerRoomPollGrain>(_fakes, _db, VOTER);

    private sealed record PollBody(
        int roomId,
        string headline,
        string summary,
        bool npsPoll,
        bool enabled,
        object[] questions
    );

    private static PollBody Poll(params object[] questions) =>
        new(ROOM_ID, "Tell us", "A question or two", false, true, questions);

    private static object Choice(int? id, string text) =>
        new
        {
            id,
            parent = (int?)null,
            sortOrder = 0,
            type = "single",
            text,
            category = 0,
            answerType = 0,
            choices = new[]
            {
                new
                {
                    id = (int?)null,
                    value = "yes",
                    text = "Yes",
                    choiceType = 0,
                },
                new
                {
                    id = (int?)null,
                    value = "no",
                    text = "No",
                    choiceType = 0,
                },
            },
        };

    private static object Text(int? id, string text) =>
        new
        {
            id,
            parent = (int?)null,
            sortOrder = 1,
            type = "line",
            text,
            category = 0,
            answerType = 0,
            choices = Array.Empty<object>(),
        };

    private async Task<int> SaveAsync(int? id, PollBody body)
    {
        var saved = await SendAsync(
            id is null ? HttpMethod.Post : HttpMethod.Put,
            id is null ? "/api/polls" : $"/api/polls/{id}",
            body
        );

        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync(Ct));

        return (await JsonAsync(saved)).GetProperty("id").GetInt32();
    }

    private async Task<string?> MessageAsync(PollBody body)
    {
        var refused = await SendAsync(HttpMethod.Post, "/api/polls", body);

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        return (await JsonAsync(refused)).GetProperty("message").GetString();
    }

    private async Task<JsonElement> GetAsync(string path) =>
        await JsonAsync(await SendAsync(HttpMethod.Get, path));

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body = null
    )
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}{path}");

        request.Headers.Authorization = new("Bearer", "a-session");

        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType());

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
