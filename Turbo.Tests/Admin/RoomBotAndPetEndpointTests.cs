using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Bots;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// A room's bots and pets on its page (<c>/api/rooms/{id}/bots</c>, <c>/pets</c>): any staff member
/// who may look up rooms sees the ones standing in it, and no other room's; setting one or taking it
/// out needs what the room's settings need - owning the room or <c>room.control.any</c> - and goes
/// through that room. Its furniture (<c>/furniture</c>, 2026-10-26) likewise, taking an item out
/// needing what picking up any furniture in a room needs, <c>room.furni.pickup_any</c>.
/// </summary>
public sealed class RoomBotAndPetEndpointTests : IAsyncDisposable
{
    private const int ROOM = 9;
    private const int OTHER_ROOM = 10;
    private const int OWNER = 2;
    private const int BOT = 31;
    private const int PET = 41;
    private const int CHAIR = 51;

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly int _port = TestPorts.Free();
    private readonly IHostedService _server;

    /// <summary>The nodes the staff member holds.</summary>
    private ImmutableHashSet<string> _holds =
    [
        PermissionNodes.Admin.PANEL,
        PermissionNodes.Admin.ROOMS_VIEW,
    ];

    public RoomBotAndPetEndpointTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = OWNER,
                Name = "owner",
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
        _db.Insert(Room(ROOM, "bot room"));
        _db.Insert(Room(OTHER_ROOM, "other room"));
        _db.Insert(Bot(BOT, "Greeter", ROOM));
        _db.Insert(Bot(32, "Elsewhere", OTHER_ROOM));
        _db.Insert(Bot(33, "Packed away", null));
        _db.Insert(Pet(PET, "Rex", ROOM));
        _db.Insert(Pet(42, "Elsewhere", OTHER_ROOM));
        _db.Insert(Definition());
        _db.Insert(Item(CHAIR, ROOM));
        _db.Insert(Item(52, OTHER_ROOM));
        _db.Insert(Item(53, null));

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call =>
            Task.FromResult(_holds.Contains((string)call.Args[0]!));
        _fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY with { Granted = _holds });
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();
        _fakes.Handlers["StaffUpdateBotAsync"] = call => Task.FromResult((int)call.Args[0]! == BOT);
        _fakes.Handlers["StaffPickupBotAsync"] = call => Task.FromResult((int)call.Args[0]! == BOT);
        _fakes.Handlers["StaffUpdatePetAsync"] = call => Task.FromResult((int)call.Args[0]! == PET);
        _fakes.Handlers["StaffPickupPetAsync"] = call => Task.FromResult((int)call.Args[0]! == PET);
        _fakes.Handlers["StaffPickupItemAsync"] = call =>
            Task.FromResult((int)call.Args[0]! == CHAIR);

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
    public async Task A_room_lists_the_bots_standing_in_it_and_no_others()
    {
        await _server.StartAsync(Ct);

        var (status, body) = await SendAsync(HttpMethod.Get, $"rooms/{ROOM}/bots");

        status.Should().Be(HttpStatusCode.OK);

        var bots = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();

        bots.Should().ContainSingle();
        bots[0].GetProperty("id").GetInt32().Should().Be(BOT);
        bots[0].GetProperty("name").GetString().Should().Be("Greeter");
        bots[0].GetProperty("ownerName").GetString().Should().Be("owner");
    }

    [Fact]
    public async Task Setting_or_taking_out_a_bot_needs_control_of_the_room_and_goes_through_it()
    {
        await _server.StartAsync(Ct);

        var edit = new
        {
            name = "Doorman",
            motto = "Hi",
            figure = "hd-180-1",
        };

        (await SendAsync(HttpMethod.Put, $"rooms/{ROOM}/bots/{BOT}", edit))
            .Status.Should()
            .Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, $"rooms/{ROOM}/bots/{BOT}/pickup"))
            .Status.Should()
            .Be(HttpStatusCode.Forbidden);
        _fakes.Log.Of("StaffUpdateBotAsync").Should().BeEmpty();
        _fakes.Log.Of("StaffPickupBotAsync").Should().BeEmpty();

        _holds = _holds.Add(PermissionNodes.Room.CONTROL_ANY);

        (await SendAsync(HttpMethod.Put, $"rooms/{ROOM}/bots/{BOT}", edit))
            .Status.Should()
            .Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Put, $"rooms/{ROOM}/bots/32", edit))
            .Status.Should()
            .Be(HttpStatusCode.BadRequest, "the room refuses a bot not standing in it");
        (await SendAsync(HttpMethod.Post, $"rooms/{ROOM}/bots/{BOT}/pickup"))
            .Status.Should()
            .Be(HttpStatusCode.OK);

        var update = _fakes.Log.Of("StaffUpdateBotAsync").First();

        update.Key.Should().Be((long)ROOM);
        ((BotStaffEditSnapshot)update.Args[1]!).Name.Should().Be("Doorman");
        _fakes
            .Log.Of("StaffPickupBotAsync")
            .Should()
            .ContainSingle()
            .Which.Key.Should()
            .Be((long)ROOM);
    }

    [Fact]
    public async Task A_room_lists_the_pets_standing_in_it_and_no_others()
    {
        await _server.StartAsync(Ct);

        var (status, body) = await SendAsync(HttpMethod.Get, $"rooms/{ROOM}/pets");

        status.Should().Be(HttpStatusCode.OK);

        var pets = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();

        pets.Should().ContainSingle();
        pets[0].GetProperty("id").GetInt32().Should().Be(PET);
        pets[0].GetProperty("name").GetString().Should().Be("Rex");
        pets[0].GetProperty("ownerName").GetString().Should().Be("owner");
    }

    [Fact]
    public async Task Setting_or_taking_out_a_pet_needs_control_of_the_room_and_sends_only_what_changed()
    {
        await _server.StartAsync(Ct);

        var edit = new { name = "Buddy" };

        (await SendAsync(HttpMethod.Put, $"rooms/{ROOM}/pets/{PET}", edit))
            .Status.Should()
            .Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, $"rooms/{ROOM}/pets/{PET}/pickup"))
            .Status.Should()
            .Be(HttpStatusCode.Forbidden);
        _fakes.Log.Of("StaffUpdatePetAsync").Should().BeEmpty();
        _fakes.Log.Of("StaffPickupPetAsync").Should().BeEmpty();

        _holds = _holds.Add(PermissionNodes.Room.CONTROL_ANY);

        (await SendAsync(HttpMethod.Put, $"rooms/{ROOM}/pets/{PET}", edit))
            .Status.Should()
            .Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Put, $"rooms/{ROOM}/pets/42", edit))
            .Status.Should()
            .Be(HttpStatusCode.BadRequest, "the room refuses a pet not standing in it");
        (await SendAsync(HttpMethod.Post, $"rooms/{ROOM}/pets/{PET}/pickup"))
            .Status.Should()
            .Be(HttpStatusCode.OK);

        var update = _fakes.Log.Of("StaffUpdatePetAsync").First();
        var sent = (PetStaffEditSnapshot)update.Args[1]!;

        update.Key.Should().Be((long)ROOM);
        sent.Name.Should().Be("Buddy");
        sent.Energy.Should().BeNull("a value not sent is left as the pet has it");
        _fakes
            .Log.Of("StaffPickupPetAsync")
            .Should()
            .ContainSingle()
            .Which.Key.Should()
            .Be((long)ROOM);
    }

    [Fact]
    public async Task A_room_lists_its_furniture_and_taking_one_out_needs_pickup_any()
    {
        await _server.StartAsync(Ct);

        var (status, body) = await SendAsync(HttpMethod.Get, $"rooms/{ROOM}/furniture");

        status.Should().Be(HttpStatusCode.OK);

        var items = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();

        items.Should().ContainSingle();
        items[0].GetProperty("id").GetInt32().Should().Be(CHAIR);
        items[0].GetProperty("className").GetString().Should().Be("chair_norja");
        items[0].GetProperty("logic").GetString().Should().Be("default_floor");
        items[0].GetProperty("ownerName").GetString().Should().Be("owner");
        items[0].GetProperty("extraData").GetString().Should().Be("1");

        (await SendAsync(HttpMethod.Post, $"rooms/{ROOM}/furniture/{CHAIR}/pickup"))
            .Status.Should()
            .Be(HttpStatusCode.Forbidden);
        _fakes.Log.Of("StaffPickupItemAsync").Should().BeEmpty();

        _holds = _holds.Add(PermissionNodes.Room.FURNI_PICKUP_ANY);

        (await SendAsync(HttpMethod.Post, $"rooms/{ROOM}/furniture/52/pickup"))
            .Status.Should()
            .Be(HttpStatusCode.Conflict, "the room refuses an item not standing in it");
        (await SendAsync(HttpMethod.Post, $"rooms/{ROOM}/furniture/{CHAIR}/pickup"))
            .Status.Should()
            .Be(HttpStatusCode.OK);

        var pickup = _fakes.Log.Of("StaffPickupItemAsync").Last();

        pickup.Key.Should().Be((long)ROOM);
        pickup.Args[1].Should().Be(1, "the staff member who took it out is its cause's actor");
    }

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(
        HttpMethod method,
        string path,
        object? body = null
    )
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}/api/{path}");

        if (body is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"
            );

        request.Headers.Authorization = new("Bearer", "a-session");

        var response = await _client.SendAsync(request, Ct);

        return (response.StatusCode, await response.Content.ReadAsStringAsync(Ct));
    }

    private static RoomEntity Room(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
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

    private static BotEntity Bot(int id, string name, int? roomId) =>
        new()
        {
            Id = id,
            PlayerEntityId = OWNER,
            RoomEntityId = roomId,
            Name = name,
            Motto = "",
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
        };

    private static FurnitureDefinitionEntity Definition() =>
        new()
        {
            Id = 1,
            SpriteId = 30,
            Name = "chair_norja",
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            Logic = "default_floor",
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = false,
            CanWalk = false,
            CanSit = true,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
        };

    private static FurnitureEntity Item(int id, int? roomId) =>
        new()
        {
            Id = id,
            PlayerEntityId = OWNER,
            FurnitureDefinitionEntityId = 1,
            RoomEntityId = roomId,
            X = 1,
            Y = 2,
            ExtraData = "1",
        };

    private static PetEntity Pet(int id, string name, int? roomId) =>
        new()
        {
            Id = id,
            PlayerEntityId = OWNER,
            RoomEntityId = roomId,
            Name = name,
            TypeId = 0,
            PaletteId = 1,
            BreedId = 0,
            Color = "FFFFFF",
        };
}
