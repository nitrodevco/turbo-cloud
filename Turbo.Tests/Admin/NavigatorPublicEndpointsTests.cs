using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's Public tab editor (<c>/api/content/navigator/public</c>): seeing it is the content
/// view, changing it needs <c>navigator.public.manage</c> on top of <c>content.manage</c>, and
/// every change, refused ones too, is in the command log.
/// </summary>
public sealed class NavigatorPublicEndpointsTests : IAsyncDisposable
{
    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly int _port;
    private readonly IHostedService _server;

    private Func<string, bool> _holds = _ => true;

    public NavigatorPublicEndpointsTests()
    {
        _port = TestPorts.Free();

        _db.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "staff",
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
        _db.Insert(
            new RoomEntity
            {
                Id = 7,
                Name = "Welcome Lounge",
                PlayerEntityId = 1,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Open,
                UsersNow = 0,
                PlayersMax = 25,
                WallHeight = -1,
                HideWalls = false,
                AllowBlocking = false,
                AllowPets = true,
                AllowPetsEat = true,
                ThicknessWall = RoomThicknessType.Normal,
                ThicknessFloor = RoomThicknessType.Normal,
                TradeType = RoomTradeModeType.Disabled,
                MuteType = ModSettingType.Owner,
                KickType = ModSettingType.Owner,
                BanType = ModSettingType.Owner,
                ChatFloodType = ChatFloodSensitivityType.Minimal,
                PlayerEntity = null!,
                RoomModelEntity = null!,
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
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(_holds((string)call.Args[0]!));
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();

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
    public async Task Changing_the_public_tab_needs_its_node_and_every_change_is_logged()
    {
        _holds = node => node != PermissionNodes.Navigator.PUBLIC_MANAGE;
        await _server.StartAsync(Ct);

        var seen = await SendAsync(HttpMethod.Get, "/api/content/navigator/public");

        seen.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(seen))
            .GetProperty("canManage")
            .GetBoolean()
            .Should()
            .BeFalse("the panel shows the page read-only without the node");
        (
            await SendAsync(
                HttpMethod.Post,
                "/api/content/navigator/public/categories",
                new { name = "Official Rooms" }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        _holds = _ => true;

        var created = await SendAsync(
            HttpMethod.Post,
            "/api/content/navigator/public/categories",
            new { name = "Official Rooms", forceClosed = true }
        );
        var id = (await JsonAsync(created)).GetProperty("id").GetInt32();

        (
            await SendAsync(
                HttpMethod.Post,
                $"/api/content/navigator/public/categories/{id}/rooms",
                new { roomId = 7 }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);
        (
            await SendAsync(
                HttpMethod.Post,
                $"/api/content/navigator/public/categories/{id}/rooms",
                new { roomId = 404 }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);

        var list = await JsonAsync(
            await SendAsync(HttpMethod.Get, "/api/content/navigator/public")
        );

        list.GetProperty("canManage").GetBoolean().Should().BeTrue();

        var category = list.GetProperty("categories")
            .EnumerateArray()
            .Single(x => x.GetProperty("id").GetInt32() == id);

        category.GetProperty("searchCode").GetString().Should().Be($"official_{id}");
        category.GetProperty("forceClosed").GetBoolean().Should().BeTrue();
        category.GetProperty("viewMode").GetInt32().Should().Be(1);
        category
            .GetProperty("rooms")
            .EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .Should()
            .Equal("Welcome Lounge");

        await using var db = await _db.CreateDbContextAsync(Ct);
        db.CommandLogs.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Command,
                x.Arguments,
                x.Outcome,
            })
            .ToList()
            .Should()
            .BeEquivalentTo(
                [
                    new
                    {
                        Command = "navigator",
                        Arguments = "public category add Official Rooms",
                        Outcome = "completed",
                    },
                    new
                    {
                        Command = "navigator",
                        Arguments = $"public room add {id} 7 first",
                        Outcome = "completed",
                    },
                    new
                    {
                        Command = "navigator",
                        Arguments = $"public room add {id} 404 first",
                        Outcome = "refused",
                    },
                ],
                options => options.WithStrictOrdering()
            );
    }

    /// <summary>The page's preview is the viewer's own <c>official_view</c> search, the blocks and rooms players get, in order.</summary>
    [Fact]
    public async Task The_preview_is_the_public_tab_as_the_navigator_answers_it()
    {
        _fakes.Handlers["SearchAsync"] = call =>
            Task.FromResult(
                ImmutableArray.Create(
                    new NavigatorSearchResultBlockSnapshot
                    {
                        SearchCode = "official_3",
                        Text = "Games",
                        ActionAllowed = NavigatorActionAllowedType.Collapsed,
                        Localization = string.Empty,
                        ForceClosed = true,
                        ViewMode = NavigatorViewModeType.Tiles,
                        Results =
                        [
                            new NavigatorSearchResultSnapshot(
                                (
                                    (RoomInfoSnapshot)
                                        RuntimeHelpers.GetUninitializedObject(
                                            typeof(RoomInfoSnapshot)
                                        )
                                ) with
                                {
                                    RoomId = 7,
                                    Name = "Welcome Lounge",
                                    OwnerName = "staff",
                                    PlayersMax = 25,
                                    DoorMode = RoomDoorModeType.Open,
                                    Tags = [],
                                }
                            )
                            {
                                Population = 4,
                            },
                        ],
                    }
                )
            );
        await _server.StartAsync(Ct);

        var preview = await JsonAsync(
            await SendAsync(HttpMethod.Get, "/api/content/navigator/public/preview")
        );

        _fakes
            .Log.Of("SearchAsync")
            .Single()
            .Args.Take(3)
            .Should()
            .Equal(new PlayerId(1), "official_view", "");
        preview
            .GetProperty("blocks")
            .EnumerateArray()
            .Select(x =>
                (
                    x.GetProperty("searchCode").GetString(),
                    x.GetProperty("text").GetString(),
                    x.GetProperty("forceClosed").GetBoolean(),
                    string.Join(
                        ',',
                        x.GetProperty("rooms")
                            .EnumerateArray()
                            .Select(r =>
                                $"{r.GetProperty("roomId").GetInt32()}:{r.GetProperty("users").GetInt32()}"
                            )
                    )
                )
            )
            .Should()
            .Equal(("official_3", "Games", true, "7:4"));
    }

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
