using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Guilds;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Forums.Enums;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// A group's forum on its panel page (2026-10-26: staff could not see whether a group had a forum
/// or who could read and post in it): the group's detail carries it, and its settings are set
/// through the forum, which holds them to the forum settings window's own rules.
/// </summary>
public sealed class GroupForumEndpointTests : IAsyncDisposable
{
    private const int GROUP = 10;
    private const int NO_FORUM = 11;
    private const int OWNER = 2;

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly int _port = TestPorts.Free();
    private readonly IHostedService _server;

    /// <summary>Whether the forum takes the settings it is sent, as its own rules would.</summary>
    private bool _forumTakes = true;

    public GroupForumEndpointTests()
    {
        foreach (var (id, name) in new[] { (1, "staff"), (OWNER, "owner") })
            _db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = name,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );

        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );
        _db.Insert(Room(8));
        _db.Insert(Room(9));
        _db.Insert(Group(GROUP, "With a forum", 8));
        _db.Insert(Group(NO_FORUM, "Without one", 9));
        _db.Insert(
            new GuildForumEntity
            {
                Id = 1,
                GuildEntityId = GROUP,
                ThreadCount = 3,
                MessageCount = 12,
            }
        );

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = _ => Task.FromResult(true);
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();
        _fakes.Handlers["UpdateSettingsAsync"] = call => TakeSettingsAsync(call);

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
    public async Task A_group_s_detail_carries_its_forum_or_none()
    {
        await _server.StartAsync(Ct);

        var forum = (
            await JsonAsync(await SendAsync(HttpMethod.Get, $"groups/{GROUP}"))
        ).GetProperty("forum");

        forum.GetProperty("readPermission").GetInt32().Should().Be(0);
        forum.GetProperty("postMessagePermission").GetInt32().Should().Be(1);
        forum.GetProperty("moderatePermission").GetInt32().Should().Be(2);
        forum.GetProperty("threadCount").GetInt32().Should().Be(3);
        forum.GetProperty("messageCount").GetInt32().Should().Be(12);
        (await JsonAsync(await SendAsync(HttpMethod.Get, $"groups/{NO_FORUM}")))
            .GetProperty("forum")
            .ValueKind.Should()
            .Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Forum_settings_go_through_the_forum_and_a_refusal_says_why()
    {
        await _server.StartAsync(Ct);

        var settings = new
        {
            readPermission = 1,
            postMessagePermission = 2,
            postThreadPermission = 2,
            moderatePermission = 3,
        };

        (await SendAsync(HttpMethod.Put, $"groups/{NO_FORUM}/forum", settings))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
        _fakes.Log.Of("UpdateSettingsAsync").Should().BeEmpty();

        var saved = await SendAsync(HttpMethod.Put, $"groups/{GROUP}/forum", settings);

        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(saved)).GetProperty("moderatePermission").GetInt32().Should().Be(3);

        var call = _fakes.Log.Of("UpdateSettingsAsync").Single();

        call.Key.Should().Be((long)GROUP);
        call.Args[0].Should().Be(new Turbo.Primitives.Players.PlayerId(1));

        _forumTakes = false;

        (await SendAsync(HttpMethod.Put, $"groups/{GROUP}/forum", settings with { }))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "the same settings again are already saved");
        (
            await SendAsync(
                HttpMethod.Put,
                $"groups/{GROUP}/forum",
                new
                {
                    readPermission = 2,
                    postMessagePermission = 1,
                    postThreadPermission = 1,
                    moderatePermission = 2,
                }
            )
        ).StatusCode.Should().Be(HttpStatusCode.BadRequest, "the forum refused them");
    }

    /// <summary>The forum's answer: the settings written when it takes them.</summary>
    private async Task TakeSettingsAsync(FakeCall call)
    {
        if (!_forumTakes)
            return;

        await using var db = await _db.CreateDbContextAsync(Ct);
        var forum = await db.GuildForums.SingleAsync(
            x => x.GuildEntityId == (int)(long)call.Key!,
            Ct
        );

        forum.ReadPermission = (GuildForumPermission)(int)call.Args[1]!;
        forum.PostMessagePermission = (GuildForumPermission)(int)call.Args[2]!;
        forum.PostThreadPermission = (GuildForumPermission)(int)call.Args[3]!;
        forum.ModeratePermission = (GuildForumPermission)(int)call.Args[4]!;
        await db.SaveChangesAsync(Ct);
    }

    private static RoomEntity Room(int id) =>
        new()
        {
            Id = id,
            Name = "homeroom",
            PlayerEntityId = OWNER,
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
        };

    private static GuildEntity Group(int id, string name, int roomId) =>
        new()
        {
            Id = id,
            Name = name,
            Description = "",
            BadgeCode = "b05114s19134",
            PrimaryColorId = 1,
            SecondaryColorId = 1,
            GuildType = GuildType.Regular,
            RightsLevel = GuildRightsLevel.Owner,
            PlayerEntityId = OWNER,
            RoomEntityId = roomId,
        };

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body = null
    )
    {
        var request = new HttpRequestMessage(
            method,
            $"http://127.0.0.1:{_port}/api/content/{path}"
        );

        request.Headers.Authorization = new("Bearer", "a-session");

        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
