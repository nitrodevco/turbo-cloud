using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Commands;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Bot;
using Turbo.Rooms.Object.Avatars.Pet;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Highscore;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>
/// <c>:ejectpets</c>, <c>:resetscores</c> and <c>:mutepets</c>: the Flash client sends these lines
/// as chat on purpose (<c>SessionDataManager.ejectPets</c>, <c>resetScores</c>,
/// <c>ChatInputWidgetHandler</c>), and the server had no such commands, so they were said out
/// loud to the room.
/// </summary>
public class PetAndScoreCommandsTests
{
    private const int OWNER = 1;
    private const int BUILDER = 2;
    private const int VISITOR = 3;
    private const int OWNERS_PET = 50;
    private const int VISITORS_PET = 51;

    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly CommandRoomFixture _room = new();

    public PetAndScoreCommandsTests()
    {
        _room.Harness.State.IsRightsLoaded = true;
        _room.Commands.Register([
            new EjectPetsCommand(),
            new ResetScoresCommand(),
            new MutePetsCommand(),
            new MuteBotsCommand(),
        ]);
        _room.Harness.Fakes.Handlers["ReturnPetAsync"] = _ => Task.FromResult(true);
    }

    [Fact]
    public async Task Ejectpets_by_the_owner_sends_everybody_elses_pets_home()
    {
        _room.AddPlayer(OWNER, PermissionNodes.Command.EJECTPETS);
        AddPet(OWNERS_PET, 100, OWNER);
        AddPet(VISITORS_PET, 101, VISITOR);

        await _room.SayAsync(OWNER, ":ejectpets");

        PetIds().Should().Equal(OWNERS_PET);
        PetsReturnedTo(VISITOR).Should().Equal(VISITORS_PET);
        PetsReturnedTo(OWNER).Should().BeEmpty();
        _room.SaidToRoom().Should().BeEmpty("the line is a command, not chat");
        _room.WhispersTo(OWNER).Should().BeEmpty("the pets leaving is the answer");
    }

    [Fact]
    public async Task Ejectpets_is_refused_to_a_player_with_only_rights()
    {
        _room.AddPlayer(BUILDER, PermissionNodes.Command.EJECTPETS);
        _room.GiveRights(BUILDER);
        AddPet(VISITORS_PET, 101, VISITOR);

        await _room.SayAsync(BUILDER, ":ejectpets");

        PetIds().Should().Equal(VISITORS_PET);
        _room.SaidToRoom().Should().BeEmpty();
        _room.WhispersTo(BUILDER).Should().ContainSingle();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resetscores_empties_the_rooms_boards_for_whoever_has_rights(bool rights)
    {
        _room.AddPlayer(BUILDER, PermissionNodes.Command.RESETSCORES);
        if (rights)
            _room.GiveRights(BUILDER);
        var board = AddBoard(40);
        await board.RecordGameAsync([(25, ["alice"])], 60, DateTimeOffset.UtcNow, Ct);
        board.Entries.Should().ContainSingle();

        await _room.SayAsync(BUILDER, ":resetscores");

        board.Entries.Should().HaveCount(rights ? 0 : 1);
        _room.SaidToRoom().Should().BeEmpty("the line is a command, not chat");
        _room.WhispersTo(BUILDER).Should().HaveCount(rights ? 0 : 1);
    }

    [Fact]
    public async Task Mutepets_stops_and_restarts_pet_talk_for_the_player_alone()
    {
        _room.AddPlayer(VISITOR, PermissionNodes.Command.MUTEPETS);
        _room.AddPlayer(BUILDER);
        var pet = AddPet(VISITORS_PET, 101, VISITOR);

        await _room.SayAsync(VISITOR, ":mutepets");
        _room.SaidToRoom().Should().BeEmpty("the line is a command, not chat");
        _room.Harness.Fakes.Log.Calls.Clear();

        await PetSaysAsync(pet, "woof");
        PetLinesTo(VISITOR).Should().BeEmpty();
        PetLinesTo(BUILDER).Should().Equal("woof");

        await _room.SayAsync(VISITOR, ":mutepets");
        _room.Harness.Fakes.Log.Calls.Clear();

        await PetSaysAsync(pet, "woof");
        _room.SaidToRoom().Should().Equal("woof");
    }

    [Fact]
    public async Task A_room_set_to_mute_all_pets_sends_no_one_what_pets_say()
    {
        _room.AddPlayer(VISITOR);
        _room.AddPlayer(BUILDER);
        var pet = AddPet(VISITORS_PET, 101, VISITOR);

        var state = _room.Harness.State;
        var snapshot = state.GetType().GetProperty("RoomSnapshot", All)!;

        snapshot.SetValue(
            state,
            (RoomSnapshot)snapshot.GetValue(state)! with
            {
                MuteAllPets = true,
            }
        );
        _room.Harness.Fakes.Log.Calls.Clear();

        await PetSaysAsync(pet, "woof");

        _room.SaidToRoom().Should().BeEmpty();
        PetLinesTo(VISITOR).Should().BeEmpty();
        PetLinesTo(BUILDER).Should().BeEmpty();
    }

    [Fact]
    public async Task Mutebots_stops_and_restarts_bot_talk_for_the_player_alone()
    {
        _room.AddPlayer(VISITOR, PermissionNodes.Command.MUTEBOTS);
        _room.AddPlayer(BUILDER);
        var bot = AddBot(102);

        await _room.SayAsync(VISITOR, ":mutebots");
        _room.SaidToRoom().Should().BeEmpty("the line is a command, not chat");
        _room.Harness.Fakes.Log.Calls.Clear();

        await _room.Harness.Room.ChatSystem.SayAsAvatarAsync(bot, "beep", new AvatarSpeech(), Ct);
        PetLinesTo(VISITOR).Should().BeEmpty();
        PetLinesTo(BUILDER).Should().Equal("beep");

        await _room.SayAsync(VISITOR, ":mutebots");
        _room.Harness.Fakes.Log.Calls.Clear();

        await _room.Harness.Room.ChatSystem.SayAsAvatarAsync(bot, "beep", new AvatarSpeech(), Ct);
        _room.SaidToRoom().Should().Equal("beep");
    }

    private IRoomBot AddBot(int objectId)
    {
        var bot = new RoomBotAvatar
        {
            ObjectId = objectId,
            BotId = objectId,
            OwnerId = OWNER,
            OwnerName = $"player{OWNER}",
            RoomId = 1,
            Skills = [],
        };
        bot.SetPosition(2, 2);
        var state = _room.Harness.State;

        state.AvatarsByObjectId[objectId] = bot;
        state.Bots.Add(bot);
        _room.Harness.Module<RoomMapModule>().AddAvatar(bot, false);

        return bot;
    }

    private Task PetSaysAsync(IRoomPet pet, string line) =>
        _room.Harness.Room.ChatSystem.SayAsAvatarAsync(pet, line, new AvatarSpeech(), Ct);

    private IEnumerable<string> PetLinesTo(int playerId) =>
        _room.Harness.Fakes.SentToPlayer<ChatMessageComposer>(playerId).Select(c => c.Text);

    private IRoomPet AddPet(int petId, int objectId, int ownerId)
    {
        var pet = RoomPetAvatar.FromSnapshot(
            objectId,
            new PetSnapshot
            {
                Id = petId,
                OwnerId = ownerId,
                OwnerName = $"player{ownerId}",
                RoomId = null,
                Name = "pet",
                Figure = new PetFigureSnapshot
                {
                    TypeId = PetTypes.HORSE,
                    PaletteId = 0,
                    Color = "FFFFFF",
                    BreedId = 0,
                    CustomParts = [],
                },
                Level = 4,
                Experience = 0,
                Energy = 60,
                Nutrition = 70,
                Respect = 0,
                RarityLevel = 0,
                HasSaddle = false,
                AnyoneCanRide = false,
                HasBreedingPermission = false,
                X = 1,
                Y = 1,
                Z = 0,
                Rotation = 0,
                CreatedAtUtc = DateTime.UtcNow,
                WateredAtUtc = DateTime.UtcNow,
                HarvestedAtUtc = null,
            }
        );
        pet.SetLogic(_room.Harness.Fakes.Create<IRoomPetLogic>(objectId));
        var state = _room.Harness.State;

        state.AvatarsByObjectId[objectId] = pet;
        state.AvatarsByPetId[petId] = objectId;
        state.Pets.Add(pet);
        _room.Harness.Module<RoomMapModule>().AddAvatar(pet, false);

        return pet;
    }

    private FurnitureHighscoreLogic AddBoard(int id)
    {
        var stuffData = _room.Harness.Services.GetRequiredService<IStuffDataFactory>();
        var item = new RoomFloorItem
        {
            ObjectId = id,
            OwnerId = OWNER,
            OwnerName = $"player{OWNER}",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 1001,
                SpriteId = 1001,
                Name = "highscore_classic*1",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "highscore",
                TotalStates = 2,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.FromInt(100),
                CanStack = true,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Everybody,
                ExtraData = null,
            },
        };

        item.SetExtraData(null);
        item.SetPosition(3, 3);
        item.SetPositionZ(Altitude.Zero);
        item.SetRotation(Rotation.North);
        var logic = new FurnitureHighscoreLogic(
            stuffData,
            new RoomFloorItemContext(_room.Harness.Room, item)
        );
        item.SetLogic(logic);

        _room.Harness.State.ItemsById.Add((RoomObjectId)id, item);
        _room.Harness.Module<RoomMapModule>().AddItem(item);

        return logic;
    }

    private IEnumerable<int> PetIds() => _room.Harness.State.Pets.Select(x => x.PetId);

    private IEnumerable<int> PetsReturnedTo(int playerId) =>
        _room
            .Harness.Fakes.Log.Of("ReturnPetAsync")
            .Where(c => c.Interface == typeof(IInventoryGrain) && Equals(c.Key, (long)playerId))
            .Select(c => ((PetSnapshot)c.Args[0]!).Id);
}
