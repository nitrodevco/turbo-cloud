using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Database.Extensions;
using Turbo.Navigator.Configuration;
using Turbo.Navigator.Onboarding;
using Turbo.Players;
using Turbo.Players.Configuration;
using Turbo.Players.Onboarding;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Messages.Outgoing.Nux;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Onboarding;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// The new user flow as Habbo ran it for a new account on 2026-10-12 (recorded with QX Scripter):
/// login lists the steps still to do, the name box answers with Habbo's codes and suggestions,
/// "I'm ready" sets the name once, and the room pick makes the furnished starter room.
/// </summary>
public sealed class NewUserOnboardingTests : IDisposable
{
    private const int NEW_PLAYER = 1;
    private const int OTHER_PLAYER = 2;
    private const int ROOM = 50;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public NewUserOnboardingTests()
    {
        _db.Insert(Player(NEW_PLAYER, "dhabbob0-5-81", pending: true));
        _db.Insert(Player(OTHER_PLAYER, "Hutch", pending: false));
    }

    public void Dispose() => _db.Dispose();

    private static PlayerEntity Player(int id, string name, bool pending) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
            OnboardingNamePending = pending,
            OnboardingRoomPending = pending,
        };

    private PlayerOnboardingService Onboarding(OnboardingConfig? config = null) =>
        new(
            _db,
            _fakes.Create<IGrainFactory>(),
            new PassThroughWordFilter(),
            Options.Create(config ?? new OnboardingConfig { Enabled = true })
        );

    [Fact]
    public void TheNameAnswers_AreWrittenAsHabboSentThem()
    {
        // CheckUserNameResult for "Admin", byte for byte as recorded from Habbo.
        var taken = PacketHarness.Encode(
            new CheckUserNameResultMessageComposer
            {
                Result = NameCheckResult.Taken,
                Name = "Admin",
                Suggestions = ["MattAdmin", "ZestyAdminCool", "HotAdminTheGeek"],
            }
        );

        taken.PopInt().Should().Be(4);
        taken.PopString().Should().Be("Admin");
        taken.PopInt().Should().Be(3);
        taken.PopString().Should().Be("MattAdmin");

        // SelectInitialRoom for room 80462556: 0000 04CBC2DC.
        var room = PacketHarness.Encode(
            new SelectInitialRoomEventMessageComposer { Status = 0, RoomId = new RoomId(80462556) }
        );

        room.PopShort().Should().Be(0);
        room.PopInt().Should().Be(80462556);
    }

    [Fact]
    public void ThePicksAndNames_AreReadAsTheClientSendsThem()
    {
        var pick = PacketHarness.Parse(
            PacketHarness.Incoming("SelectInitialRoomMessageEvent"),
            PacketHarness.Payload(w => w.String("10"))
        );
        var name = PacketHarness.Parse(
            PacketHarness.Incoming("ChangeUserNameMessageEvent"),
            PacketHarness.Payload(w => w.String("Hutch."))
        );

        pick.Should()
            .BeOfType<Turbo.Primitives.Messages.Incoming.Nux.SelectInitialRoomMessage>()
            .Which.RoomType.Should()
            .Be("10");
        name.Should()
            .BeOfType<Turbo.Primitives.Messages.Incoming.Avatar.ChangeUserNameMessage>()
            .Which.Name.Should()
            .Be("Hutch.");
    }

    [Fact]
    public async Task Login_ListsTheStepsStillToDo_AndNoneWhenTheFlowIsOff()
    {
        (await Onboarding().GetLoginActionsAsync(NEW_PLAYER, Ct)).Should().Equal(0, 1);
        (await Onboarding().GetLoginActionsAsync(OTHER_PLAYER, Ct)).Should().BeEmpty();
        (await Onboarding(new OnboardingConfig()).GetLoginActionsAsync(NEW_PLAYER, Ct))
            .Should()
            .BeEmpty();
        (
            await Onboarding(new OnboardingConfig { Enabled = true, ChooseName = false })
                .GetLoginActionsAsync(NEW_PLAYER, Ct)
        )
            .Should()
            .Equal(1);
    }

    [Fact]
    public async Task ANameCheck_AnswersWithHabbosCodes()
    {
        var onboarding = Onboarding();

        (await onboarding.CheckNameAsync(NEW_PLAYER, "Ad", Ct))
            .Result.Should()
            .Be(NameCheckResult.TooShort);
        (await onboarding.CheckNameAsync(NEW_PLAYER, "ab cd", Ct))
            .Result.Should()
            .Be(NameCheckResult.NotAllowed);
        (await onboarding.CheckNameAsync(NEW_PLAYER, "Hutch.", Ct))
            .Result.Should()
            .Be(NameCheckResult.Available);

        // Their own name is theirs to keep.
        (await onboarding.CheckNameAsync(NEW_PLAYER, "DHABBOB0-5-81", Ct))
            .Result.Should()
            .Be(NameCheckResult.Available);

        // Once the step is done, a free name is 6, as Habbo answered after the account was named.
        (await onboarding.CheckNameAsync(OTHER_PLAYER, "Hutch.", Ct))
            .Result.Should()
            .Be(NameCheckResult.ChangeNotAllowed);
    }

    [Fact]
    public async Task ATakenName_IsAnswered4_WithThreeFreeNamesMadeFromIt()
    {
        _db.Insert(Player(3, "HutchCool", pending: false));

        var check = await Onboarding().CheckNameAsync(NEW_PLAYER, "hutch", Ct);

        check.Result.Should().Be(NameCheckResult.Taken);
        check.Suggestions.Should().HaveCount(3);
        check
            .Suggestions.Should()
            .OnlyContain(x => x.Contains("hutch", StringComparison.OrdinalIgnoreCase))
            .And.NotContain(x => x.Equals("HutchCool", StringComparison.OrdinalIgnoreCase))
            .And.OnlyContain(x => PlayerNames.Check(x) == null);
    }

    [Fact]
    public async Task ChoosingAName_RenamesThePlayerOnce()
    {
        var grain = (IPlayerGrain)
            GrainHarness.Create(
                typeof(PlayerModule).Assembly,
                "Turbo.Players.Grains.PlayerGrain",
                _fakes,
                _db,
                NEW_PLAYER
            );

        (await grain.ChooseNameAsync("Hutch.", Ct)).Should().Be(NameCheckResult.Available);
        (await grain.ChooseNameAsync("Other", Ct)).Should().Be(NameCheckResult.ChangeNotAllowed);

        await using var db = await _db.CreateDbContextAsync(Ct);
        var player = await db.Players.SingleAsync(x => x.Id == NEW_PLAYER, Ct);

        player.Name.Should().Be("Hutch.");
        player.NameKey.Should().Be("hutch.");
        player.OnboardingNamePending.Should().BeFalse();
        player.OnboardingRoomPending.Should().BeTrue();
        _fakes.Log.Of("SetPlayerNameAsync").Should().ContainSingle();
    }

    [Fact]
    public async Task ChoosingATakenName_KeepsTheStepOpen()
    {
        var grain = (IPlayerGrain)
            GrainHarness.Create(
                typeof(PlayerModule).Assembly,
                "Turbo.Players.Grains.PlayerGrain",
                _fakes,
                _db,
                NEW_PLAYER
            );

        (await grain.ChooseNameAsync("Hutch", Ct)).Should().Be(NameCheckResult.Taken);

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.Players.SingleAsync(x => x.Id == NEW_PLAYER, Ct))
            .OnboardingNamePending.Should()
            .BeTrue();
    }

    [Fact]
    public async Task TheRoomStep_EndsOnce()
    {
        var onboarding = Onboarding();

        (await onboarding.IsRoomPendingAsync(NEW_PLAYER, Ct)).Should().BeTrue();
        (await onboarding.CompleteRoomAsync(NEW_PLAYER, Ct)).Should().BeTrue();
        (await onboarding.CompleteRoomAsync(NEW_PLAYER, Ct)).Should().BeFalse();
        (await onboarding.IsRoomPendingAsync(NEW_PLAYER, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task APlayerMadeWhileTheFlowIsOn_HasBothSteps()
    {
        var accounts = new Turbo.Players.Accounts.PlayerAccountService(
            _db,
            _fakes.Create<Turbo.Primitives.Players.Accounts.IOwnerBootstrap>(),
            new PassThroughWordFilter(),
            Options.Create(new OnboardingConfig { Enabled = true }),
            NullLogger<Turbo.Primitives.Players.Accounts.IPlayerAccountService>.Instance
        );

        var created = await accounts.CreateAsync(
            new Turbo.Primitives.Players.Accounts.NewPlayer(
                "Newbie",
                null,
                AvatarGenderType.Male,
                null
            ),
            Ct
        );

        await using var db = await _db.CreateDbContextAsync(Ct);
        var player = await db.Players.SingleAsync(x => x.Id == created.Created!.Value.Value, Ct);

        player.OnboardingNamePending.Should().BeTrue();
        player.OnboardingRoomPending.Should().BeTrue();
    }

    [Fact]
    public async Task StarterRoom10_IsMadeAsHabboFurnishedIt()
    {
        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model_h",
                Model = "x",
                DoorX = 4,
                DoorY = 4,
                DoorRotation = Rotation.SouthEast,
                Enabled = true,
                Custom = false,
            }
        );
        _db.Insert(
            new RoomEntity
            {
                Id = ROOM,
                Name = "placeholder",
                PlayerEntityId = NEW_PLAYER,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Open,
                UsersNow = 0,
                PlayersMax = 25,
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
            }
        );

        string[] floor =
        [
            "pura_nt_mdl36",
            "cubie_nt_shelf_2_b",
            "cubie_nt_shelf_1_b",
            "usva_nt_shelf",
            "pixel_bed_blue",
            "pura_nt_mdl26",
            "anna_nt_table_blue",
            "chair_nt_polyfon",
            "pixel_floor_silver",
            "pura_nt_mdl16",
        ];
        string[] wall = ["val09_nt_wdrobe_g", "window_nt_diner2", "usva_nt_wallrug"];
        Dictionary<string, FurnitureDefinitionEntity> definitions = [];
        var nextId = 100;

        foreach (var name in floor.Concat(wall))
        {
            var definition = Definition(nextId++, name, wall.Contains(name));
            definitions[name] = definition;
            _db.Insert(definition);
        }

        // pixel_clock_blue is left out: a hotel without it gets the room without it.
        _fakes.Handlers["TryGetDefinitionByName"] = call =>
            definitions.TryGetValue((string)call.Args[0]!, out var d) ? d.ToSnapshot(0.01) : null;
        _fakes.Handlers["GetPlayerNameAsync"] = _ => Task.FromResult("Hutch.");

        string? madeName = null;
        int? madeUsers = null;
        _fakes.Handlers["CreateRoomAsync"] = call =>
        {
            madeName = (string)call.Args[1]!;
            madeUsers = (int)call.Args[5]!;
            return Task.FromResult<RoomId?>(new RoomId(ROOM));
        };

        var starterRooms = new StarterRoomService(
            _fakes.Create<INavigatorService>(),
            _fakes.Create<IFurnitureDefinitionProvider>(),
            _db,
            _fakes.Create<IGrainFactory>(),
            Options.Create(new NavigatorConfig()),
            NullLogger<IStarterRoomService>.Instance
        );

        starterRooms.HasType("10").Should().BeTrue();
        starterRooms.HasType("99").Should().BeFalse();

        var made = await starterRooms.CreateAsync(NEW_PLAYER, "10", Ct);

        made.Should().Be(new CreatedStarterRoom(new RoomId(ROOM), "Hutch.'s  room"));
        madeName.Should().Be("Hutch.'s  room");
        madeUsers.Should().Be(new NavigatorConfig().MaxPlayersLimit);

        await using var db = await _db.CreateDbContextAsync(Ct);
        var room = await db.Rooms.SingleAsync(x => x.Id == ROOM, Ct);
        var items = await db.Furnitures.Where(x => x.RoomEntityId == ROOM).ToListAsync(Ct);

        room.PaintWall.Should().Be("2403");
        room.PaintFloor.Should().Be("610");
        room.PaintLandscape.Should().Be("0.0");
        items.Should().HaveCount(20);
        items.Should().OnlyContain(x => x.PlayerEntityId == NEW_PLAYER);

        var bed = items.Single(x =>
            x.FurnitureDefinitionEntityId == definitions["pixel_bed_blue"].Id
        );
        bed.X.Should().Be(3);
        bed.Y.Should().Be(10);
        bed.Rotation.Should().Be(Rotation.East);

        var shelf = items.First(x =>
            x.FurnitureDefinitionEntityId == definitions["cubie_nt_shelf_1_b"].Id
        );
        shelf.ExtraData.Should().Contain("\"4\"");

        var wardrobe = items.Single(x =>
            x.FurnitureDefinitionEntityId == definitions["val09_nt_wdrobe_g"].Id
        );
        wardrobe.X.Should().Be(4);
        wardrobe.Y.Should().Be(8);
        wardrobe.WallOffset.Should().Be(0);
        wardrobe.Rotation.Should().Be(Rotation.West);
    }

    private static FurnitureDefinitionEntity Definition(int id, string name, bool wall) =>
        new()
        {
            Id = id,
            SpriteId = id,
            Name = name,
            ProductType = wall ? ProductType.Wall : ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            Logic = "default_floor",
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = false,
            CanGroup = true,
            CanSell = false,
        };
}
