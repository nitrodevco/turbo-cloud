using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Navigator.Onboarding;
using Turbo.Players.Configuration;
using Turbo.Players.Onboarding;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Onboarding;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// What Habbo showed a new account on 2026-10-12 after the new user flow (recorded on
/// hhus): login says the player is a "real noob" (<c>NoobnessLevel</c> 2); "Enter the room!"
/// and the toolbar ask for <c>predefined_noob_lobby</c> and land in a door mode 4 room only new
/// users enter; and the Introduction reward track behind "Start your Habbo Journey!".
/// </summary>
public sealed class NewUserLobbyTests : IDisposable
{
    private const int NEW_PLAYER = 1;
    private const int OLD_PLAYER = 2;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public NewUserLobbyTests()
    {
        _db.Insert(Player(NEW_PLAYER, "Hutch", DateTime.UtcNow.AddHours(-1)));
        _db.Insert(Player(OLD_PLAYER, "Bill", DateTime.UtcNow.AddDays(-3)));
        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model_a",
                Model = "x",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.South,
                Enabled = true,
                Custom = false,
            }
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Login_says_a_player_who_signed_up_within_the_hours_is_a_real_noob()
    {
        var onboarding = Onboarding(new OnboardingConfig { NewUserHours = 24 });

        (await onboarding.GetNoobnessLevelAsync(NEW_PLAYER, Ct))
            .Should()
            .Be(NoobnessLevelType.RealNoob);
        (await onboarding.GetNoobnessLevelAsync(OLD_PLAYER, Ct))
            .Should()
            .Be(NoobnessLevelType.NotNoob);
        (
            await Onboarding(new OnboardingConfig { NewUserHours = 0 })
                .GetNoobnessLevelAsync(NEW_PLAYER, Ct)
        )
            .Should()
            .Be(NoobnessLevelType.NotNoob, "0 makes no one new");
    }

    [Fact]
    public async Task A_new_user_lobby_lets_in_only_new_users()
    {
        var harness = new LiveRoomHarness();
        var snapshot = RoomHarness.GetMember(harness.State, "RoomSnapshot")!;
        RoomHarness.SetMember(snapshot, "DoorMode", RoomDoorModeType.NoobLobby);
        harness.Fakes.Handlers["GetNoobnessLevelAsync"] = call =>
            Task.FromResult(
                (PlayerId)call.Args[0]! == NEW_PLAYER
                    ? NoobnessLevelType.RealNoob
                    : NoobnessLevelType.NotNoob
            );

        var entry = harness.Module<RoomEntryModule>();

        (await entry.CheckAccessAsync(NEW_PLAYER, null, false))
            .Should()
            .Be(RoomEntryAccessType.Allowed);
        (await entry.CheckAccessAsync(OLD_PLAYER, null, false))
            .Should()
            .Be(RoomEntryAccessType.NewUsersOnly);
    }

    [Fact]
    public async Task The_room_settings_window_cannot_make_a_room_a_new_user_lobby()
    {
        var harness = new LiveRoomHarness();
        var logger = new CapturingLogger<Turbo.Primitives.Rooms.Grains.IRoomGrain>();
        RoomHarness.SetField(harness.Room, "_logger", logger);
        RoomHarness.SetMember(
            RoomHarness.GetMember(harness.State, "RoomSnapshot")!,
            "OwnerId",
            (PlayerId)OLD_PLAYER
        );

        var result = await harness.Room.SaveRoomSettingsAsync(
            Turbo.Primitives.Action.ActionContext.CreateForPlayer(OLD_PLAYER, new RoomId(1)),
            new Turbo.Primitives.Rooms.Snapshots.Settings.RoomSettingsUpdateSnapshot
            {
                Name = "My room",
                Description = "",
                DoorMode = (int)RoomDoorModeType.NoobLobby,
                Password = "",
                MaximumVisitors = 25,
                CategoryId = null,
                Tags = [],
                TradeMode = RoomTradeModeType.Disabled,
                AllowPets = false,
                AllowPetsEat = false,
                AllowWalkThrough = false,
                HideWalls = false,
                WallThickness = RoomThicknessType.Normal,
                FloorThickness = RoomThicknessType.Normal,
                WhoCanMute = ModSettingType.Owner,
                WhoCanKick = ModSettingType.Owner,
                WhoCanBan = ModSettingType.Owner,
                ChatProtection = ChatFloodSensitivityType.Normal,
                LeaveOnDoorTile = false,
                IdleSleepEnabled = false,
                IdleSleepTimeoutSeconds = 0,
                IdleAutokickEnabled = false,
                IdleAutokickTimeoutSeconds = 0,
                MuteAllPets = false,
            },
            Ct
        );

        result.Error.Should().Be(Turbo.Primitives.Rooms.Enums.RoomSettingsSaveErrorType.Invalid);
        logger
            .AtLeast(Microsoft.Extensions.Logging.LogLevel.Warning)
            .Should()
            .Contain(x => x.Message.Contains("door mode"));
    }

    [Fact]
    public async Task The_noob_lobby_link_goes_to_the_fullest_lobby_with_room_left()
    {
        _db.Insert(Room(10, "Lobby A", RoomDoorModeType.NoobLobby, max: 10));
        _db.Insert(Room(11, "Lobby B", RoomDoorModeType.NoobLobby, max: 10));
        _db.Insert(Room(12, "Lobby C (full)", RoomDoorModeType.NoobLobby, max: 5));
        _db.Insert(Room(13, "Busy room", RoomDoorModeType.Open, max: 50));

        var population = new Dictionary<int, int>
        {
            [10] = 2,
            [11] = 4,
            [12] = 5,
            [13] = 30,
        };
        _fakes.Handlers["GetRoomPopulationAsync"] = call =>
            Task.FromResult(population[((RoomId)call.Args[0]!).Value]);

        var lobbies = Lobbies();

        (await lobbies.PickAsync(Ct)).Should().Be(new RoomId(11));

        population[11] = 10;
        (await lobbies.PickAsync(Ct)).Should().Be(new RoomId(10));

        population[10] = 10;
        (await lobbies.PickAsync(Ct)).Should().BeNull("every lobby is full");
    }

    [Fact]
    public async Task Making_a_lobby_turns_the_clients_lobbies_on_and_the_last_one_going_turns_them_off()
    {
        _db.Insert(Room(10, "Lobby", RoomDoorModeType.Open, max: 10));

        var variables = new Dictionary<string, string>();
        _fakes.Handlers["SaveAsync"] = call =>
        {
            variables[(string)call.Args[0]!] = (string)call.Args[1]!;

            return Fakes.NotHandled;
        };
        // The room grain's own change, as it lands in the database.
        _fakes.Handlers["SetNoobLobbyAsync"] = call =>
        {
            using var db = _db.CreateDbContext();
            var room = db.Rooms.Single(x => x.Id == 10);
            var lobby = (bool)call.Args[0]!;

            if ((room.DoorMode == RoomDoorModeType.NoobLobby) == lobby)
                return Task.FromResult(false);

            room.DoorMode = lobby ? RoomDoorModeType.NoobLobby : RoomDoorModeType.Open;
            db.SaveChanges();

            return Task.FromResult(true);
        };

        var lobbies = Lobbies();

        (await lobbies.SetAsync(new RoomId(10), true, NEW_PLAYER, Ct))
            .Should()
            .Be(NoobLobbyChange.Added);
        variables[NoobLobbyDirectory.LOBBIES_ENABLED_VARIABLE].Should().Be("true");

        (await lobbies.SetAsync(new RoomId(10), true, NEW_PLAYER, Ct))
            .Should()
            .Be(NoobLobbyChange.AlreadyAdded);

        (await lobbies.SetAsync(new RoomId(10), false, NEW_PLAYER, Ct))
            .Should()
            .Be(NoobLobbyChange.Removed);
        variables[NoobLobbyDirectory.LOBBIES_ENABLED_VARIABLE].Should().Be("false");
    }

    [Fact]
    public async Task ForwardToSomeRoom_predefined_noob_lobby_forwards_to_the_lobby_picked()
    {
        _db.Insert(Room(10, "Lobby", RoomDoorModeType.NoobLobby, max: 10));
        _db.Insert(Room(11, "Busy room", RoomDoorModeType.Open, max: 10));

        var packets = new PacketHarness();
        packets.Resolver.Overrides[typeof(INoobLobbyDirectory)] = new NoobLobbyDirectory(
            _db,
            packets.Fakes.Create<IGrainFactory>(),
            packets.Fakes.Create<Turbo.Primitives.Gamedata.IGamedataVariableService>()
        );

        await packets.SendAsync(
            PacketHarness.Incoming("ForwardToSomeRoomMessageEvent"),
            PacketHarness.Payload(w => w.String(INoobLobbyDirectory.FORWARD_DATA)),
            playerId: NEW_PLAYER
        );

        packets.Fakes.Log.Of("GetRandomPopulatedRoomAsync").Should().BeEmpty();
        packets
            .Fakes.Log.Of("ForwardToRoomAsync")
            .Should()
            .ContainSingle()
            .Which.Args[0]
            .Should()
            .Be(new RoomId(10));
    }

    private NoobLobbyDirectory Lobbies() =>
        new(
            _db,
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<Turbo.Primitives.Gamedata.IGamedataVariableService>()
        );

    private PlayerOnboardingService Onboarding(OnboardingConfig config) =>
        new(
            _db,
            _fakes.Create<IGrainFactory>(),
            new PassThroughWordFilter(),
            Options.Create(config),
            new Turbo.Players.Accounts.PlayerNamePolicy(Options.Create(new PlayerConfig()))
        );

    private static PlayerEntity Player(int id, string name, DateTime createdAt) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
            CreatedAt = createdAt,
        };

    private static RoomEntity Room(int id, string name, RoomDoorModeType doorMode, int max) =>
        new()
        {
            Id = id,
            Name = name,
            PlayerEntityId = OLD_PLAYER,
            RoomModelEntityId = 1,
            DoorMode = doorMode,
            UsersNow = 0,
            PlayersMax = max,
            WallHeight = -1,
            HideWalls = false,
            ThicknessWall = RoomThicknessType.Normal,
            ThicknessFloor = RoomThicknessType.Normal,
            AllowBlocking = false,
            AllowPets = false,
            AllowPetsEat = false,
            TradeType = RoomTradeModeType.Disabled,
            MuteType = ModSettingType.Owner,
            KickType = ModSettingType.Owner,
            BanType = ModSettingType.Owner,
            ChatFloodType = ChatFloodSensitivityType.Minimal,
            PlayerEntity = null!,
            RoomModelEntity = null!,
        };
}
