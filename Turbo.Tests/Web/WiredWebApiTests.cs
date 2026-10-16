using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Authentication;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Players.Accounts;
using Turbo.Primitives.Action;
using Turbo.Primitives.Authentication;

using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Accounts;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Turbo.Web.Accounts;
using Turbo.Web.Configuration;
using Turbo.Web.Discord;
using Turbo.Web.Sessions;
using Xunit;

namespace Turbo.Tests.Web;

/// <summary>
/// The public Web API's Wired Variables endpoints (Habbo's OpenAPI document, tag "Wired
/// Variables") over real HTTP against a live room holding a "Variable Global Add-on: Web API"
/// (<c>wf_xtra_var_web_api</c>, Flash <c>VariablesWebApiAddon</c>): the keys its box issues,
/// reading and writing values as 64-bit decimal strings, wall and Builders Club ids, batches,
/// profiles, mass deletion and the per-room limits.
/// </summary>
public sealed class WiredWebApiTests : IAsyncDisposable
{
    private const int ROOM = 1;
    private const int ADDON = 2;
    private const int FURNI_VAR = 3;
    private const int USER_VAR = 4;
    private const int GLOBAL_VAR = 5;
    private const int CHAIR = 10;
    private const int WALL_LAMP = 11;
    private const int PLAYER_INDEX = 7;

    // The room calls every player WiredRoom.Enter seats 100 + their room index.
    private const int PLAYER_ID = 100 + PLAYER_INDEX;

    private readonly WiredRoom _room = new(8, 8);
    private readonly SqliteDb _db = new();
    private readonly int _port;
    private readonly IHostedService _server;
    private readonly HttpClient _http;
    private readonly WebConfig _config;
    private long _nowMs;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WiredWebApiTests()
        : this(new WiredApiConfig()) { }

    private WiredWebApiTests(WiredApiConfig wiredApi)
    {
        using (var free = new TcpListener(IPAddress.Loopback, 0))
        {
            free.Start();
            _port = ((IPEndPoint)free.LocalEndpoint).Port;
        }

        _config = new WebConfig
        {
            Enabled = true,
            Url = $"http://127.0.0.1:{_port}",
            WiredApi = wiredApi,
        };
        _room.Harness.Fakes.Instances[(typeof(IRoomGrain), (long)ROOM)] = _room.Harness.Room;
        InsertRoom();
        _server = Build();
        _http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}") };
    }

    [Fact]
    public async Task A_room_without_the_add_on_has_no_api_and_an_unknown_room_is_not_found()
    {
        await _server.StartAsync(Ct);

        (await SendAsync(HttpMethod.Get, "/api/public/rooms/1/variables", read: "x"))
            .Should()
            .Be((403, "wired.variables.api_disabled"));
        (await SendAsync(HttpMethod.Get, "/api/public/rooms/999/variables", read: "x"))
            .Should()
            .Be((404, "room.not_found"));
    }

    [Fact]
    public async Task The_box_only_saves_keys_it_issued_and_hides_them_from_others()
    {
        var addon = _room.AddBox<WiredAddonVariablesWebApi>(ADDON, 0, 0, "wf_xtra_var_web_api");
        var read = await IssueAsync(true);

        (
            await _room.SaveWithResultAsync<UpdateAddonMessage>(
                ADDON,
                intParams: [0],
                stringParam: "chosen-by-hand\t"
            )
        )
            .ErrorKey.Should()
            .Be(WiredSaveErrors.INVALID_API_KEYS);
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                ADDON,
                intParams: [1],
                stringParam: read + "\t"
            )
        )
            .Should()
            .BeTrue();

        addon.ReadKey.Should().Be(read);
        // "Allow mass deletion" needs a write key (VariablesWebApiAddon.readIntParamsFromForm).
        addon.AllowsBulkDelete.Should().BeFalse();
        addon.GetPickupConfirmation()!.BodyKey.Should().Be("wiredfurni.web_api.pickup.body");
    }

    [Fact]
    public async Task Keys_are_checked_and_values_keep_all_64_bits()
    {
        var (read, write) = await SetUpAsync();

        (await SendAsync(HttpMethod.Get, "/api/public/rooms/1/variables"))
            .Should()
            .Be((403, "wired.variables.key_missing"));
        (await SendAsync(HttpMethod.Get, "/api/public/rooms/1/variables", read: write))
            .Should()
            .Be((403, "wired.variables.key_invalid"));

        var list = await JsonAsync(HttpMethod.Get, "/api/public/rooms/1/variables", read: read);

        list!["users"]!.AsArray().Select(x => (string)x!).Should().Equal("points");
        list["furni"]!.AsArray().Select(x => (string)x!).Should().Equal("hp");
        list["global"]!.AsArray().Select(x => (string)x!).Should().Equal("round");

        // 2^53 + 1: a JSON number would lose it, the decimal string keeps it.
        var put = await JsonAsync(
            HttpMethod.Put,
            $"/api/public/rooms/1/variables/furni/hp/furni/{CHAIR}",
            write: write,
            body: """{"value":"9007199254740993"}"""
        );

        put!["value"]!.GetValue<string>().Should().Be("9007199254740993");
        put["creation_time"]!.GetValue<string>().Should().EndWith("Z");
        (
            await JsonAsync(
                HttpMethod.Get,
                $"/api/public/rooms/1/variables/furni/hp/furni/{CHAIR}",
                read: read
            )
        )!["value"]!
            .GetValue<string>()
            .Should()
            .Be("9007199254740993");
        (
            await SendAsync(
                HttpMethod.Put,
                $"/api/public/rooms/1/variables/furni/hp/furni/{CHAIR}",
                write: read,
                body: """{"value":"1"}"""
            )
        )
            .Should()
            .Be((403, "wired.variables.key_invalid"));
        (
            await SendAsync(
                HttpMethod.Put,
                $"/api/public/rooms/1/variables/furni/hp/users/{CHAIR}",
                write: write,
                body: """{"value":"1"}"""
            )
        )
            .Should()
            .Be((400, "wired.variables.invalid_target"));
        (
            await SendAsync(
                HttpMethod.Put,
                $"/api/public/rooms/1/variables/furni/hp/furni/{CHAIR}",
                write: write,
                body: """{"value":"1.5"}"""
            )
        )
            .Should()
            .Be((400, "wired.variables.invalid_value"));
        (
            await SendAsync(
                HttpMethod.Delete,
                $"/api/public/rooms/1/variables/furni/hp/furni/{CHAIR}",
                write: write
            )
        )
            .Should()
            .Be((204, null));
        (
            await SendAsync(
                HttpMethod.Get,
                $"/api/public/rooms/1/variables/furni/hp/furni/{CHAIR}",
                read: read
            )
        )
            .Should()
            .Be((404, "wired.variables.not_found"));
    }

    [Fact]
    public async Task Wall_items_go_by_their_positive_id_and_globals_are_patched()
    {
        var (read, write) = await SetUpAsync();

        (
            await JsonAsync(
                HttpMethod.Put,
                $"/api/public/rooms/1/variables/furni/hp/wall-items/{WALL_LAMP}",
                write: write,
                body: """{"value":"-4"}"""
            )
        )!["value"]!
            .GetValue<string>()
            .Should()
            .Be("-4");
        // A wall item is not a floor furni.
        (
            await SendAsync(
                HttpMethod.Get,
                $"/api/public/rooms/1/variables/furni/hp/furni/{WALL_LAMP}",
                read: read
            )
        )
            .Should()
            .Be((404, "wired.variables.not_found"));

        (
            await JsonAsync(
                HttpMethod.Patch,
                "/api/public/rooms/1/variables/global/round",
                write: write,
                body: """{"value":"3"}"""
            )
        )!["value"]!
            .GetValue<string>()
            .Should()
            .Be("3");
        (await JsonAsync(HttpMethod.Get, "/api/public/rooms/1/variables_profile/global", read: read))!
            ["variables"]!["round"]!["value"]!
            .GetValue<string>()
            .Should()
            .Be("3");
    }

    [Fact]
    public async Task A_batch_answers_every_operation_with_its_op_id()
    {
        var (read, write) = await SetUpAsync();

        var results = (
            await JsonAsync(
                HttpMethod.Post,
                "/api/public/rooms/1/variables/user/points/batch",
                read: read,
                write: write,
                body: $$$"""
                {"requests":[
                  {"op_id":"put","method":"PUT","path":"users/{{{PLAYER_ID}}}","body":{"value":"7"}},
                  {"op_id":"get","method":"GET","path":"users/{{{PLAYER_ID}}}"},
                  {"op_id":"bad","method":"GET","path":"furni/{{{CHAIR}}}"},
                  {"method":"DELETE","path":"users/{{{PLAYER_ID}}}"}
                ]}
                """
            )
        )!["results"]!.AsArray();

        results.Select(x => (string?)x!["op_id"]).Should().Equal("put", "get", "bad", null);
        results.Select(x => (int)x!["status"]!).Should().Equal(200, 200, 400, 204);
        results[1]!["body"]!["value"]!.GetValue<string>().Should().Be("7");
        results[2]!["error"]!["code"]!.GetValue<string>().Should().Be("wired.variables.invalid_target");

        var tooMany = new JsonArray(
            [.. Enumerable.Range(0, 51).Select(_ => (JsonNode)new JsonObject { ["method"] = "GET", ["path"] = $"users/{PLAYER_ID}" })]
        );

        (
            await SendAsync(
                HttpMethod.Post,
                "/api/public/rooms/1/variables/user/points/batch",
                read: read,
                write: write,
                body: new JsonObject { ["requests"] = tooMany }.ToJsonString()
            )
        )
            .Should()
            .Be((400, "wired.variables.batch_limit_exceeded"));
    }

    [Fact]
    public async Task Profiles_list_what_an_entity_holds_and_patch_several_at_once()
    {
        var (read, write) = await SetUpAsync();

        var profile = await JsonAsync(
            HttpMethod.Patch,
            $"/api/public/rooms/1/variables_profile/user/users/{PLAYER_ID}",
            write: write,
            body: """{"variables":{"points":"12"}}"""
        );

        profile!["user"]!["id"]!.GetValue<int>().Should().Be(PLAYER_ID);
        profile["variables"]!["points"]!["value"]!.GetValue<string>().Should().Be("12");
        (
            await JsonAsync(
                HttpMethod.Patch,
                $"/api/public/rooms/1/variables_profile/user/users/{PLAYER_ID}",
                write: write,
                body: """{"variables":{"points":null}}"""
            )
        )!["variables"]!
            .AsObject()
            .Should()
            .BeEmpty();
        (
            await SendAsync(
                HttpMethod.Get,
                "/api/public/rooms/1/variables_profile/furni/furni/12345",
                read: read
            )
        )
            .Should()
            .Be((404, "wired.variables.entity_not_found"));
        (await JsonAsync(
            HttpMethod.Get,
            $"/api/public/rooms/1/variables/user/points/users/count",
            read: read
        ))!["count"]!.GetValue<int>().Should().Be(0);
    }

    [Fact]
    public async Task Mass_deletion_only_works_when_the_box_allows_it()
    {
        var (_, write) = await SetUpAsync(allowBulkDelete: false);

        (
            await SendAsync(
                HttpMethod.Post,
                "/api/public/rooms/1/variables/bulk-delete",
                write: write,
                body: """{"variables":["hp"]}"""
            )
        )
            .Should()
            .Be((403, "wired.variables.bulk_delete_not_enabled"));
    }

    [Fact]
    public async Task Each_room_has_its_own_limit_per_kind_of_call()
    {
        await using var limited = new WiredWebApiTests(
            new WiredApiConfig { Writes = new WiredApiLimit { PerMinute = 120, Burst = 2 } }
        );
        var (_, write) = await limited.SetUpAsync();
        var path = $"/api/public/rooms/1/variables/furni/hp/furni/{CHAIR}";
        var body = """{"value":"1"}""";

        (await limited.SendAsync(HttpMethod.Put, path, write: write, body: body)).Status.Should().Be(200);
        (await limited.SendAsync(HttpMethod.Put, path, write: write, body: body)).Status.Should().Be(200);
        (await limited.SendAsync(HttpMethod.Put, path, write: write, body: body))
            .Should()
            .Be((429, "wired.variables.too_many_requests"));
    }

    private async Task<(string read, string write)> SetUpAsync(bool allowBulkDelete = true)
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        _room.AddFloorItem(CHAIR, 2, 2);
        _room.AddWallItem(WALL_LAMP);
        await AddVariableAsync<WiredVariableFurni>(FURNI_VAR, 5, 5, "wf_var_furni", [1, (int)WiredAvailabilityType.RoomActive], "hp");
        await AddVariableAsync<WiredVariableUser>(USER_VAR, 5, 6, "wf_var_user", [(int)WiredAvailabilityType.UserActive, 1], "points");
        await AddVariableAsync<WiredVariableRoom>(GLOBAL_VAR, 5, 7, "wf_var_room", [(int)WiredAvailabilityType.RoomActive], "round");

        _room.AddBox<WiredAddonVariablesWebApi>(ADDON, 0, 0, "wf_xtra_var_web_api");
        var read = await IssueAsync(true);
        var write = await IssueAsync(false);

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                ADDON,
                intParams: [allowBulkDelete ? 1 : 0],
                stringParam: read + "\t" + write
            )
        )
            .Should()
            .BeTrue();

        await _server.StartAsync(Ct);

        return (read, write);
    }

    private async Task<string> IssueAsync(bool isReadKey) =>
        (
            await _room.Harness.Room.GenerateWiredWebApiKeyAsync(
                ActionContext.CreateForSystem(ROOM),
                ADDON,
                isReadKey,
                Ct
            )
        )!;

    private async Task AddVariableAsync<T>(int id, int x, int y, string logic, int[] intParams, string name)
        where T : FurnitureWiredVariableLogic
    {
        var box = _room.AddBox<T>(id, x, y, logic);

        (await _room.SaveAsync<UpdateVariableMessage>(id, intParams: intParams, stringParam: name))
            .Should()
            .BeTrue();
        await box.LoadWiredAsync(Ct);
        await _room
            .Harness.Module<RoomWiredSystem>()
            .OnRoomEventAsync(
                new Turbo.Primitives.Rooms.Events.Wired.RoomWiredStackChangedEvent
                {
                    RoomId = ROOM,
                    CausedBy = ActionContext.CreateForSystem(ROOM),
                    StackIds = [_room.Map.ToIdx(x, y)],
                },
                Ct
            );
        await _room.Harness.Module<RoomWiredSystem>().ProcessWiredAsync(_nowMs += 10_000, dormant: false, Ct);
    }

    private async Task<(int Status, string? Error)> SendAsync(
        HttpMethod method,
        string path,
        string? read = null,
        string? write = null,
        string? body = null
    )
    {
        var (status, json) = await RawAsync(method, path, read, write, body);

        return (status, json?["error"]?.GetValue<string>());
    }

    private async Task<JsonNode?> JsonAsync(
        HttpMethod method,
        string path,
        string? read = null,
        string? write = null,
        string? body = null
    )
    {
        var (status, json) = await RawAsync(method, path, read, write, body);

        status.Should().Be(200, json?.ToJsonString());

        return json;
    }

    private async Task<(int, JsonNode?)> RawAsync(
        HttpMethod method,
        string path,
        string? read,
        string? write,
        string? body
    )
    {
        using var request = new HttpRequestMessage(method, path);

        if (read is not null)
            request.Headers.Add("X-Wired-Read-Key", read);

        if (write is not null)
            request.Headers.Add("X-Wired-Write-Key", write);

        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);

        return ((int)response.StatusCode, text.Length == 0 ? null : JsonNode.Parse(text));
    }

    private void InsertRoom()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "alice",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Female,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model_a",
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
                Id = ROOM,
                Name = "Variables",
                PlayerEntityId = 1,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Open,
                UsersNow = 0,
                PlayersMax = 25,
                WallHeight = -1,
                HideWalls = false,
                ThicknessWall = RoomThicknessType.Normal,
                ThicknessFloor = RoomThicknessType.Normal,
                AllowBlocking = true,
                AllowPets = true,
                AllowPetsEat = true,
                TradeType = RoomTradeModeType.Disabled,
                MuteType = ModSettingType.Owner,
                KickType = ModSettingType.Owner,
                BanType = ModSettingType.Owner,
                ChatFloodType = ChatFloodSensitivityType.Minimal,
                LastActive = DateTime.UtcNow,
                PlayerEntity = null!,
                RoomModelEntity = null!,
            }
        );
    }

    private IHostedService Build()
    {
        IOptions<WebConfig> config = Options.Create(_config);
        var fakes = new Fakes();
        var services = new ServiceCollection();

        services.AddSingleton(config);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IDbContextFactory<TurboDbContext>>(_db);
        services.AddSingleton(_room.Grains);
        services.AddSingleton(fakes.Create<IOwnerBootstrap>());
        services.AddSingleton(Options.Create(new Turbo.Players.Configuration.OnboardingConfig()));
        services.AddSingleton<IPlayerNamePolicy>(
            new PlayerNamePolicy(Options.Create(new Turbo.Players.Configuration.PlayerConfig()))
        );
        services.AddSingleton<IPlayerAccountService, PlayerAccountService>();
        services.AddSingleton<ILoginTicketService, LoginTicketService>();
        services.AddSingleton(fakes.Create<ISanctionService>());
        services.AddSingleton<IWordFilter, PassThroughWordFilter>();
        services.AddSingleton(sp => new DiscordOAuthClient(
            new HttpClient(),
            config,
            NullLogger<DiscordOAuthClient>.Instance
        ));
        services.AddSingleton<WebAccounts>();
        services.AddSingleton<WebSessions>();
        services.AddSingleton<PendingSignUps>();

        var serverType = typeof(WebConfig).Assembly.GetType("Turbo.Web.Api.WebApiServer")!;

        return (IHostedService)
            Activator.CreateInstance(
                serverType,
                services.BuildServiceProvider(),
                config,
                NullLoggerFactory.Instance,
                Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(serverType))
            )!;
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _server.StopAsync(CancellationToken.None);
        _db.Dispose();
    }
}
