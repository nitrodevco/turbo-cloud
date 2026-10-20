using System.Collections.Immutable;
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
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Players.Accounts;
using Turbo.Primitives.Action;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Furniture.Enums;
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
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.WebApi;
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
        _port = TestPorts.Free();

        _config = new WebConfig
        {
            Enabled = true,
            Url = $"http://127.0.0.1:{_port}",
            WiredApi = wiredApi,
        };
        _room.Harness.Fakes.Instances[(typeof(IRoomGrain), (long)ROOM)] = _room.Harness.Room;
        // The room under test is loaded; any other room is asleep until a call wakes it.
        _room.Harness.Fakes.Handlers["GetActiveRoomIdsAsync"] = _ =>
            Task.FromResult(ImmutableArray.Create(new RoomId(ROOM)));
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
        (await _room.SaveAsync<UpdateAddonMessage>(ADDON, intParams: [1], stringParam: read + "\t"))
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
    public async Task A_list_starts_at_page_1_and_page_0_is_refused()
    {
        var (read, write) = await SetUpAsync();

        await JsonAsync(
            HttpMethod.Put,
            $"/api/public/rooms/1/variables/furni/hp/furni/{CHAIR}",
            write: write,
            body: """{"value":"7"}"""
        );

        var page = await JsonAsync(
            HttpMethod.Get,
            "/api/public/rooms/1/variables/furni/hp/furni?page=1&size=2",
            read: read
        );

        page!["page"]!.GetValue<int>().Should().Be(1);
        page["items"]!.AsArray().Should().ContainSingle();
        (
            await SendAsync(
                HttpMethod.Get,
                "/api/public/rooms/1/variables/furni/hp/furni?page=0&size=2",
                read: read
            )
        )
            .Should()
            .Be((400, "wired.variables.invalid_value"));
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
        (
            await JsonAsync(
                HttpMethod.Get,
                "/api/public/rooms/1/variables_profile/global",
                read: read
            )
        )!["variables"]!["round"]!["value"]!
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
        results[2]!["error"]!["code"]!
            .GetValue<string>()
            .Should()
            .Be("wired.variables.invalid_target");

        var tooMany = new JsonArray([
            .. Enumerable
                .Range(0, 51)
                .Select(_ =>
                    (JsonNode)new JsonObject { ["method"] = "GET", ["path"] = $"users/{PLAYER_ID}" }
                ),
        ]);

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
        (
            await JsonAsync(
                HttpMethod.Get,
                $"/api/public/rooms/1/variables/user/points/users/count",
                read: read
            )
        )!["count"]!
            .GetValue<int>()
            .Should()
            .Be(0);
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
            new WiredApiConfig
            {
                Writes = new WiredApiLimit { PerMinute = 120, Burst = 2 },
            }
        );
        var (_, write) = await limited.SetUpAsync();
        var path = $"/api/public/rooms/1/variables/furni/hp/furni/{CHAIR}";
        var body = """{"value":"1"}""";

        (await limited.SendAsync(HttpMethod.Put, path, write: write, body: body))
            .Status.Should()
            .Be(200);
        (await limited.SendAsync(HttpMethod.Put, path, write: write, body: body))
            .Status.Should()
            .Be(200);
        (await limited.SendAsync(HttpMethod.Put, path, write: write, body: body))
            .Should()
            .Be((429, "wired.variables.too_many_requests"));
    }

    [Fact]
    public async Task A_room_that_is_not_loaded_is_only_woken_by_a_call_with_its_add_ons_key()
    {
        const int ASLEEP = 2;
        const int WITHOUT_ADDON = 3;

        _db.Insert(NewRoom(ASLEEP, "Asleep"));
        _db.Insert(NewRoom(WITHOUT_ADDON, "No add-on"));
        _db.Insert(AddonDefinition());
        _db.Insert(StoredAddon(50, ASLEEP, "stored-read\tstored-write"));
        _room.Harness.Fakes.Handlers["HandleWiredWebApiAsync"] = _ =>
            Task.FromResult(new WiredWebApiResponse(200, "{}"));
        await _server.StartAsync(Ct);

        var asleep = $"/api/public/rooms/{ASLEEP}/variables";

        (await SendAsync(HttpMethod.Get, asleep)).Should().Be((403, "wired.variables.key_missing"));
        (await SendAsync(HttpMethod.Get, asleep, read: "guessed"))
            .Should()
            .Be((403, "wired.variables.key_invalid"));
        (await SendAsync(HttpMethod.Get, asleep, read: "stored-write"))
            .Should()
            .Be((403, "wired.variables.key_invalid"));
        (
            await SendAsync(
                HttpMethod.Put,
                $"/api/public/rooms/{ASLEEP}/variables/furni/hp/furni/{CHAIR}",
                write: "stored-read",
                body: """{"value":"1"}"""
            )
        )
            .Should()
            .Be((403, "wired.variables.key_invalid"));
        (await SendAsync(HttpMethod.Get, $"/api/public/rooms/{WITHOUT_ADDON}/variables", read: "x"))
            .Should()
            .Be((403, "wired.variables.api_disabled"));
        _room.Harness.Fakes.Log.Calls.Should().NotContain(call => call.Interface == typeof(IRoomGrain));

        (await SendAsync(HttpMethod.Get, asleep, read: "stored-read")).Status.Should().Be(200);
        _room
            .Harness.Fakes.Log.Of("HandleWiredWebApiAsync")
            .Should()
            .ContainSingle()
            .Which.Interface.Should()
            .Be(typeof(IRoomGrain));
    }

    [Fact]
    public async Task With_two_add_ons_the_one_with_the_lowest_id_holds_the_keys()
    {
        const int LOWER_ADDON = 1;
        var (read, _) = await SetUpAsync();

        _room.AddBox<WiredAddonVariablesWebApi>(LOWER_ADDON, 0, 1, "wf_xtra_var_web_api");
        var lowerRead = (
            await _room.Harness.Room.GenerateWiredWebApiKeyAsync(
                ActionContext.CreateForSystem(ROOM),
                LOWER_ADDON,
                true,
                Ct
            )
        )!;
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                LOWER_ADDON,
                intParams: [0],
                stringParam: lowerRead + "	"
            )
        )
            .Should()
            .BeTrue();

        // Every call answers with the same box, whichever the room lists first.
        for (var i = 0; i < 3; i++)
        {
            (await SendAsync(HttpMethod.Get, "/api/public/rooms/1/variables", read: lowerRead))
                .Status.Should()
                .Be(200);
            (await SendAsync(HttpMethod.Get, "/api/public/rooms/1/variables", read: read))
                .Should()
                .Be((403, "wired.variables.key_invalid"));
        }
    }

    [Fact]
    public async Task Refused_calls_count_against_their_caller_not_the_rooms_limit()
    {
        await using var limited = new WiredWebApiTests(
            new WiredApiConfig
            {
                SimpleReads = new WiredApiLimit { PerMinute = 300, Burst = 2 },
                Failures = new WiredApiLimit { PerMinute = 60, Burst = 3 },
            }
        );
        var (read, _) = await limited.SetUpAsync();
        const string PATH = "/api/public/rooms/1/variables";

        for (var i = 0; i < 2; i++)
            (await limited.SendAsync(HttpMethod.Get, PATH, read: "guessed"))
                .Should()
                .Be((403, "wired.variables.key_invalid"));

        // The room's own allowance is untouched by them.
        (await limited.SendAsync(HttpMethod.Get, PATH, read: read)).Status.Should().Be(200);
        (await limited.SendAsync(HttpMethod.Get, PATH, read: read)).Status.Should().Be(200);
        (await limited.RetryAfterAsync(PATH, read)).Should().Be((429, true));

        // A guesser is slowed down once out of refusals, told when to come back.
        (await limited.SendAsync(HttpMethod.Get, PATH, read: "guessed"))
            .Should()
            .Be((403, "wired.variables.key_invalid"));
        (await limited.RetryAfterAsync(PATH, "guessed")).Should().Be((429, true));
    }

    [Fact]
    public async Task Batched_writes_share_a_budget_a_minute_and_reads_are_free()
    {
        await using var limited = new WiredWebApiTests(
            new WiredApiConfig { BatchWriteBudgetPerMinute = 3 }
        );
        var (read, write) = await limited.SetUpAsync();
        var put = new JsonObject
        {
            ["method"] = "PUT",
            ["path"] = $"users/{PLAYER_ID}",
            ["body"] = new JsonObject { ["value"] = "1" },
        };
        var get = new JsonObject { ["method"] = "GET", ["path"] = $"users/{PLAYER_ID}" };

        var results = (
            await limited.JsonAsync(
                HttpMethod.Post,
                "/api/public/rooms/1/variables/user/points/batch",
                read: read,
                write: write,
                body: new JsonObject
                {
                    ["requests"] = new JsonArray(
                        put.DeepClone(),
                        put.DeepClone(),
                        put.DeepClone(),
                        put.DeepClone(),
                        get.DeepClone()
                    ),
                }.ToJsonString()
            )
        )!["results"]!.AsArray();

        // The fourth write is over the budget and answered on its own; the read still runs.
        results.Select(x => (int)x!["status"]!).Should().Equal(200, 200, 200, 429, 200);
        results[3]!["error"]!["code"]!
            .GetValue<string>()
            .Should()
            .Be("wired.variables.too_many_requests");
        // A profile PATCH spends from the same budget, so it is refused whole.
        (
            await limited.SendAsync(
                HttpMethod.Patch,
                $"/api/public/rooms/1/variables_profile/user/users/{PLAYER_ID}",
                write: write,
                body: """{"variables":{"points":"2"}}"""
            )
        )
            .Should()
            .Be((429, "wired.variables.too_many_requests"));
    }

    [Fact]
    public async Task A_player_who_is_away_is_found_by_name_or_unique_id_with_their_profile()
    {
        var (read, write) = await SetUpAsync();

        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER_ID,
                Name = "bob",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        await AddVariableAsync<WiredVariableUser>(
            6,
            6,
            5,
            "wf_var_user",
            [(int)WiredAvailabilityType.Persistent, 1],
            "coins"
        );
        await JsonAsync(
            HttpMethod.Put,
            $"/api/public/rooms/1/variables/user/coins/users/{PLAYER_ID}",
            write: write,
            body: """{"value":"5"}"""
        );
        Leave(PLAYER_INDEX);

        foreach (var query in new[] { "name=BOB", $"unique_id={PLAYER_ID}" })
        {
            var profile = await JsonAsync(
                HttpMethod.Get,
                $"/api/public/rooms/1/variables_profile/user/users?{query}",
                read: read
            );

            profile!["user"]!["id"]!.GetValue<int>().Should().Be(PLAYER_ID);
            profile["user"]!["name"]!.GetValue<string>().Should().Be("bob");
            profile["user"]!["unique_id"]!.GetValue<string>().Should().Be($"{PLAYER_ID}");
            profile["variables"]!["coins"]!["value"]!.GetValue<string>().Should().Be("5");
        }

        (
            await JsonAsync(
                HttpMethod.Get,
                $"/api/public/rooms/1/variables_profile/user/users/{PLAYER_ID}",
                read: read
            )
        )!["user"]!["name"]!
            .GetValue<string>()
            .Should()
            .Be("bob");
        // alice exists but holds nothing permanent here: no profile.
        (
            await SendAsync(
                HttpMethod.Get,
                "/api/public/rooms/1/variables_profile/user/users?name=alice",
                read: read
            )
        )
            .Should()
            .Be((404, "wired.variables.entity_not_found"));
    }

    [Fact]
    public async Task A_public_room_and_a_players_rooms_answer_as_the_official_api_does()
    {
        _db.Insert(NewRoom(2, "Hidden", doorMode: RoomDoorModeType.Invisible));
        _db.Insert(
            NewRoom(
                3,
                "Shy",
                doorMode: RoomDoorModeType.Locked,
                hideOwner: true,
                tags: "games,chill"
            )
        );
        await _server.StartAsync(Ct);

        var (status, room) = await RawAsync(
            HttpMethod.Get,
            "/api/public/rooms/1",
            null,
            null,
            null
        );

        // Field order of an official client capture of GET rooms/80462556 (2026-10-16).
        status.Should().Be(200);
        room!
            .AsObject()
            .Select(x => x.Key)
            .Should()
            .Equal(
                "id",
                "name",
                "description",
                "creationTime",
                "tags",
                "maximumVisitors",
                "showOwnerName",
                "ownerName",
                "ownerUniqueId",
                "categories",
                "rating",
                "publicRoom",
                "doorMode",
                "uniqueId"
            );
        room["id"]!.GetValue<int>().Should().Be(ROOM);
        room["name"]!.GetValue<string>().Should().Be("Variables");
        room["creationTime"]!
            .GetValue<string>()
            .Should()
            .MatchRegex(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.000\+00:00$");
        room["publicRoom"]!.GetValue<bool>().Should().BeFalse();
        room["doorMode"]!.GetValue<string>().Should().Be("open");
        room["maximumVisitors"]!.GetValue<int>().Should().Be(25);
        room["ownerName"]!.GetValue<string>().Should().Be("alice");
        room["ownerUniqueId"]!.GetValue<string>().Should().Be("1");
        room["uniqueId"]!.GetValue<string>().Should().Be("1");

        // Invisible rooms are not public; a hidden owner is not named.
        (await SendAsync(HttpMethod.Get, "/api/public/rooms/2"))
            .Should()
            .Be((404, "not-found"));

        var shy = (await RawAsync(HttpMethod.Get, "/api/public/rooms/3", null, null, null)).Item2!;

        shy["showOwnerName"]!.GetValue<bool>().Should().BeFalse();
        shy.AsObject().ContainsKey("ownerName").Should().BeFalse();
        shy["doorMode"]!.GetValue<string>().Should().Be("doorbell");
        shy["tags"]!.AsArray().Select(x => (string)x!).Should().Equal("games", "chill");

        var owned = (
            await RawAsync(HttpMethod.Get, "/api/public/users/1/rooms", null, null, null)
        ).Item2!.AsArray();

        owned.Select(x => (int)x!["id"]!).Should().Equal(1, 3);
        (await SendAsync(HttpMethod.Get, "/api/public/users/999/rooms"))
            .Should()
            .Be((404, "not-found"));
        (await RawAsync(HttpMethod.Get, "/api/public/users/hhus-x/rooms", null, null, null))
            .Item2!.ToJsonString()
            .Should()
            .Be("""{"errors":[{"param":"id","msg":"user.invalid_id","value":"hhus-x"}]}""");

        _db.Insert(new PlayerSettingsEntity { PlayerEntityId = 1, ProfileHidden = true });

        // A hidden profile is not found, not an empty list.
        (await SendAsync(HttpMethod.Get, "/api/public/users/1/rooms"))
            .Should()
            .Be((404, "not-found"));
    }

    private void Leave(int roomIndex)
    {
        (
            (IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(_room.Harness.State, "AvatarsByObjectId")!
        ).Remove(roomIndex);
        (
            (IDictionary<PlayerId, RoomObjectId>)
                RoomHarness.GetMember(_room.Harness.State, "AvatarsByPlayerId")!
        ).Remove(100 + roomIndex);
    }

    private async Task<(string read, string write)> SetUpAsync(bool allowBulkDelete = true)
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        _room.AddFloorItem(CHAIR, 2, 2);
        _room.AddWallItem(WALL_LAMP);
        await AddVariableAsync<WiredVariableFurni>(
            FURNI_VAR,
            5,
            5,
            "wf_var_furni",
            [1, (int)WiredAvailabilityType.RoomActive],
            "hp"
        );
        await AddVariableAsync<WiredVariableUser>(
            USER_VAR,
            5,
            6,
            "wf_var_user",
            [(int)WiredAvailabilityType.UserActive, 1],
            "points"
        );
        await AddVariableAsync<WiredVariableRoom>(
            GLOBAL_VAR,
            5,
            7,
            "wf_var_room",
            [(int)WiredAvailabilityType.RoomActive],
            "round"
        );

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

    private async Task AddVariableAsync<T>(
        int id,
        int x,
        int y,
        string logic,
        int[] intParams,
        string name
    )
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
        await _room
            .Harness.Module<RoomWiredSystem>()
            .ProcessWiredAsync(_nowMs += 10_000, dormant: false, Ct);
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

    /// <summary>A GET's status and whether it says when to try again (<c>Retry-After</c>, in whole seconds).</summary>
    private async Task<(int Status, bool HasRetryAfter)> RetryAfterAsync(string path, string read)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Wired-Read-Key", read);

        using var response = await _http.SendAsync(request, Ct);

        return (
            (int)response.StatusCode,
            response.Headers.RetryAfter?.Delta is { TotalSeconds: >= 1 }
        );
    }

    private static FurnitureDefinitionEntity AddonDefinition() =>
        new()
        {
            Id = 1,
            SpriteId = 1,
            Name = "wf_xtra_var_web_api",
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            Logic = "wf_xtra_var_web_api",
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = false,
            CanTrade = false,
            CanGroup = false,
            CanSell = false,
        };

    /// <summary>A Web API add-on as a room that is not loaded keeps it: its keys in its stored wired data.</summary>
    private static FurnitureEntity StoredAddon(int id, int roomId, string keys) =>
        new()
        {
            Id = id,
            PlayerEntityId = 1,
            FurnitureDefinitionEntityId = 1,
            RoomEntityId = roomId,
            ExtraData = new JsonObject
            {
                ["wired"] = new JsonObject
                {
                    ["intParams"] = new JsonArray(0),
                    ["stringParam"] = keys,
                },
            }.ToJsonString(),
        };

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
        _db.Insert(NewRoom(ROOM, "Variables"));
    }

    private static RoomEntity NewRoom(
        int id,
        string name,
        RoomDoorModeType doorMode = RoomDoorModeType.Open,
        bool hideOwner = false,
        string? tags = null
    ) =>
        new()
        {
            Id = id,
            Name = name,
            PlayerEntityId = 1,
            RoomModelEntityId = 1,
            DoorMode = doorMode,
            HideOwner = hideOwner,
            Tags = tags,
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
        };

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
