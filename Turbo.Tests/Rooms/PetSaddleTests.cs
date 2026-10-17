using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The saddle a horse wears, as Habbo's furni data describes it: <c>customparams</c>
/// "&lt;petType&gt; &lt;layer&gt; &lt;part&gt; &lt;palette&gt; &lt;riderEffect&gt;" (<c>horse_saddle1</c>
/// "15 4 9 0 77", <c>horse_saddle2</c> "15 4 10 0 103"). A saddled horse carries the saddle as a
/// custom part (official client capture, room 69322463: "15 3 ffffff 3 2 -1 1 3 -1 1 4 10 0"), its
/// rider wears the saddle's effect, and taking it off gives that saddle back. Before, the horse
/// was drawn bare, every rider wore 77 and the configured saddle came back whichever was used.
/// </summary>
public sealed class PetSaddleTests
{
    private const int OWNER = 1;
    private const int PET = 40;
    private const int SADDLE = 41;
    private const int ROLLER = 42;
    private const int SADDLE1_DEFINITION = 4221;
    private const int SADDLE2_DEFINITION = 4450;
    private const string SADDLE1 = "15 4 9 0 77";
    private const string SADDLE2 = "15 4 10 0 103";

    private readonly WiredRoom _room = new(10, 10);
    private readonly RoomPlayerAvatar _owner = new() { ObjectId = 5, PlayerId = OWNER };
    private PetSnapshot _horse = Horse();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PetSaddleTests()
    {
        var harness = _room.Harness;

        _owner.SetPosition(3, 4);
        _owner.SetLogic(harness.Fakes.Create<IRoomPlayerLogic>(5));
        ((IDictionary<RoomObjectId, IRoomAvatar>)Member("AvatarsByObjectId"))[5] = _owner;
        ((IDictionary<PlayerId, RoomObjectId>)Member("AvatarsByPlayerId"))[OWNER] = 5;
        _room.Map.AddAvatar(_owner, false);
        RoomHarness.SetMember(Member("RoomSnapshot"), "AllowPets", true);
        RoomHarness.SetField(harness.Room, "_eventSystem", new TestEventBus().System);
        RoomHarness.SetField(
            harness.Room,
            "_avatarProvider",
            new RoomAvatarProvider(Options.Create(new BotConfig()))
        );

        harness.Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutPetAsync)] = _ =>
            Task.FromResult<PetSnapshot?>(_horse);
        harness.Fakes.Handlers["CanRoll"] = _ => true;
        harness.Fakes.Handlers["TryGetDefinition"] = call =>
            call.Args[0] switch
            {
                SADDLE1_DEFINITION => Definition(SADDLE1_DEFINITION, "horse_saddle1", SADDLE1),
                SADDLE2_DEFINITION => Definition(SADDLE2_DEFINITION, "horse_saddle2", SADDLE2),
                _ => null,
            };
        harness.Fakes.Handlers["TryGetDefinitionByName"] = call =>
            call.Args[0] is "horse_saddle1"
                ? Definition(SADDLE1_DEFINITION, "horse_saddle1", SADDLE1)
                : null;
    }

    private object Member(string name) => RoomHarness.GetMember(_room.Harness.State, name)!;

    private RoomPetModule Pets => _room.Harness.Module<RoomPetModule>();

    private static ActionContext Owner => ActionContext.CreateForPlayer(OWNER, 1);

    [Theory]
    [InlineData(SADDLE1_DEFINITION, SADDLE1, "15 3 FFFFFF 3 2 -1 1 3 -1 1 4 9 0", 77)]
    [InlineData(SADDLE2_DEFINITION, SADDLE2, "15 3 FFFFFF 3 2 -1 1 3 -1 1 4 10 0", 103)]
    public async Task A_saddle_is_drawn_on_the_horse_and_its_rider_wears_the_saddles_effect(
        int definitionId,
        string customParams,
        string figure,
        int effectId
    )
    {
        await PlaceAsync();

        (await SaddleAsync(definitionId, customParams)).Should().BeTrue();

        var pet = Pet();
        pet.HasSaddle.Should().BeTrue();
        ClientFigure().Should().Be(figure, "the saddle is a custom part, as Habbo sends it");

        (await Pets.MountPetAsync(Owner, PET, true, Ct)).Should().BeTrue();

        _owner.GetSnapshot().EffectId.Should().Be(effectId);
    }

    [Fact]
    public async Task Taking_the_saddle_off_gives_back_the_one_that_was_on_and_draws_the_horse_bare()
    {
        await PlaceAsync();
        (await SaddleAsync(SADDLE2_DEFINITION, SADDLE2)).Should().BeTrue();

        (await Pets.RemovePetSaddleAsync(Owner, PET, Ct)).Should().BeTrue();

        Granted().Should().Equal(SADDLE2_DEFINITION);
        Pet().HasSaddle.Should().BeFalse();
        ClientFigure().Should().Be("15 3 FFFFFF 2 2 -1 1 3 -1 1");
    }

    [Fact]
    public async Task A_horse_keeps_which_saddle_it_wears_when_picked_up_and_placed_again()
    {
        await PlaceAsync();
        (await SaddleAsync(SADDLE2_DEFINITION, SADDLE2)).Should().BeTrue();
        PetSnapshot? returned = null;
        _room.Harness.Fakes.Handlers[nameof(IInventoryGrain.ReturnPetAsync)] = call =>
        {
            returned = (PetSnapshot)call.Args[0]!;

            return Task.FromResult(true);
        };

        // What the room hands back to the inventory is what it is placed with next time.
        (await Pets.PickupPetAsync(Owner, PET, Ct))
            .Should()
            .BeTrue();
        returned!.SaddleDefinitionId.Should().Be(SADDLE2_DEFINITION);
        _horse = returned;
        await PlaceAsync();
        ClientFigure().Should().EndWith(" 4 10 0");
        (await Pets.MountPetAsync(Owner, PET, true, Ct)).Should().BeTrue();

        _owner.GetSnapshot().EffectId.Should().Be(103);
    }

    [Fact]
    public async Task A_rider_rolled_on_a_roller_stays_on_the_horse()
    {
        var roller = _room.AddFloorItem(
            ROLLER,
            4,
            4,
            "roller",
            createLogic: (factory, ctx) => new FurnitureRollerLogic(factory, ctx)
        );
        roller.SetRotation(Rotation.East);
        RoomHarness.SetMember(_room.Harness.State, "IsFurniLoaded", true);
        await _room
            .Harness.Module<RoomRollerSystem>()
            .OnRoomEventAsync(
                new RoomRollerChangedEvent
                {
                    RoomId = 1,
                    CausedBy = Owner,
                    ObjectId = ROLLER,
                },
                Ct
            );
        _horse = _horse with { HasSaddle = true };
        await PlaceAsync();
        (await Pets.MountPetAsync(Owner, PET, true, Ct)).Should().BeTrue();
        var lift = _owner.Z.Value - Pet().Z.Value;

        await _room.Harness.Module<RoomRollerSystem>().ProcessRollersAsync(10_000, Ct);
        await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(10_000, Ct);

        (_owner.X, _owner.Y).Should().Be((5, 4));
        (Pet().X, Pet().Y).Should().Be((5, 4));
        (_owner.Z.Value - Pet().Z.Value).Should().Be(lift).And.BeGreaterThan(0);
    }

    private async Task PlaceAsync() =>
        (await Pets.PlacePetAsync(Owner, PET, 4, 4, Ct)).Should().BeTrue();

    private Task<bool> SaddleAsync(int definitionId, string customParams)
    {
        _room.AddFloorItem(
            SADDLE,
            2,
            2,
            "pet_saddle",
            definitionId: definitionId,
            createLogic: (factory, ctx) => new FurniturePetSaddleLogic(factory, ctx),
            customParams: customParams
        );

        return _room.Harness.Room.InteractWithItemAsync(
            Owner,
            SADDLE,
            new UseWithPetInteraction { PetId = PET },
            Ct
        );
    }

    private IRoomPet Pet()
    {
        Pets.TryGetPet(PET, out var pet).Should().BeTrue();

        return pet!;
    }

    /// <summary>The figure the room sends the clients for the horse.</summary>
    private string ClientFigure() => ((RoomPetAvatarSnapshot)Pet().GetSnapshot()).Figure;

    private IEnumerable<int> Granted() =>
        _room
            .Harness.Fakes.Log.Calls.Where(x =>
                x.Method == nameof(IInventoryGrain.GrantFurnitureAsync)
            )
            .Select(x => (int)x.Args[0]!);

    private static FurnitureDefinitionSnapshot Definition(
        int id,
        string name,
        string customParams
    ) =>
        new()
        {
            Id = id,
            SpriteId = id,
            Name = name,
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.PetSaddle,
            LogicName = "pet_saddle",
            TotalStates = 1,
            Width = 1,
            Length = 1,
            StackHeight = Altitude.Zero,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
            UsagePolicy = FurnitureUsageType.Nobody,
            ExtraData = null,
            CustomParams = customParams,
        };

    private static PetSnapshot Horse() =>
        new()
        {
            Id = PET,
            OwnerId = OWNER,
            OwnerName = "owner",
            RoomId = null,
            Name = "Rex",
            Figure = new PetFigureSnapshot
            {
                TypeId = PetTypes.HORSE,
                PaletteId = 3,
                Color = "FFFFFF",
                BreedId = 0,
                CustomParts = [2, -1, 1, 3, -1, 1],
            },
            Level = 10,
            Experience = 0,
            Energy = 100,
            Nutrition = 100,
            Respect = 0,
            RarityLevel = 0,
            HasSaddle = false,
            AnyoneCanRide = false,
            HasBreedingPermission = false,
            X = 0,
            Y = 0,
            Z = Altitude.Zero,
            Rotation = Rotation.South,
            CreatedAtUtc = DateTime.UtcNow,
            WateredAtUtc = DateTime.UtcNow,
            HarvestedAtUtc = null,
        };
}
