using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Pet;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A monsterplant breeds once ("Plants can be bred only once!", breedpets.widget.info) until it
/// is treated with a re-breeding potion (breedpetsresult.widget.info.sorry). Our plants bred on
/// every accept, two seeds each time, and the potion did nothing.
/// </summary>
public sealed class MonsterplantBreedOnceTests
{
    private const int OWNER = 1;
    private const int PLANT = 40;
    private const int OTHER_PLANT = 41;
    private const int POTION = 60;
    private const int SEED_DEFINITION = 4300;

    private readonly RoomHarness _room = CreateRoom();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomPetModule Pets => _room.Module<RoomPetModule>();

    [Fact]
    public async Task Two_plants_breed_once_and_are_written_bred_before_the_seeds_are_given()
    {
        var plant = AddPlant(PLANT, 1);
        var other = AddPlant(OTHER_PLANT, 3);

        (await BreedAsync()).Should().BeTrue();

        plant.CanBreed.Should().BeFalse();
        other.CanBreed.Should().BeFalse();
        var calls = _room.Fakes.Log.Calls.ToList();
        var bred = calls.FindIndex(x =>
            x.Method == "WriteUseAsync"
            && x.Args[2] is List<PetSnapshot> pets
            && pets.Count(p => p.HasBred) == 2
        );
        bred.Should().BeGreaterThanOrEqualTo(0, "both plants are written bred at once");
        bred.Should()
            .BeLessThan(
                calls.FindIndex(x => x.Method == nameof(IInventoryGrain.GrantFurnitureAsync))
            );

        _room.Fakes.Log.Calls.Clear();
        (await BreedAsync()).Should().BeFalse("a plant breeds only once");
        _room
            .Fakes.Log.Calls.Should()
            .NotContain(x => x.Method == nameof(IInventoryGrain.GrantFurnitureAsync));
    }

    [Fact]
    public async Task A_breeding_counts_once_for_the_reward_tracks_and_a_refused_one_not()
    {
        // Habboween 2026 "Breed a monsterplant" (breed_mnstr, BREED_MONSTER_PLANT): both plants
        // are this owner's, so it counts once.
        AddPlant(PLANT, 1);
        AddPlant(OTHER_PLANT, 3);

        (await BreedAsync()).Should().BeTrue();
        (await BreedAsync()).Should().BeFalse();

        _room
            .Fakes.Log.Of("RecordActionAsync")
            .Select(x => x.Args[0])
            .Should()
            .Equal(RewardTrackActionTypes.BREED_MONSTER_PLANT);
    }

    [Fact]
    public async Task The_rebreeding_potion_lets_a_bred_plant_breed_again()
    {
        var plant = AddPlant(PLANT, 2);
        AddPlant(OTHER_PLANT, 2);
        (await BreedAsync()).Should().BeTrue();

        (await UsePotionAsync("{\"pet_rebreed\":{\"MaxRarityLevel\":5}}", PLANT)).Should().BeTrue();

        plant.CanBreed.Should().BeTrue();
        _room
            .Fakes.Log.Calls.ToList()
            .Exists(x =>
                x.Method == "WriteUseAsync"
                && x.Args[1] is RoomObjectId id
                && id == POTION
                && x.Args[2] is List<PetSnapshot> pets
                && pets.Any(p => p.Id == PLANT && !p.HasBred)
            )
            .Should()
            .BeTrue("the plant is written with the potion it used up");
        _room.ItemsById.Contains((RoomObjectId)POTION).Should().BeFalse();
    }

    [Theory]
    [InlineData(false, 2, "{\"pet_rebreed\":{\"MaxRarityLevel\":5}}")]
    [InlineData(true, 7, "{\"pet_rebreed\":{\"MaxRarityLevel\":5}}")]
    public async Task The_potion_is_kept_for_a_plant_that_can_breed_or_is_outside_its_band(
        bool bred,
        int rarityLevel,
        string band
    )
    {
        var plant = AddPlant(PLANT, rarityLevel);
        plant.SetHasBred(bred);

        (await UsePotionAsync(band, PLANT)).Should().BeFalse();

        _room.ItemsById.Contains((RoomObjectId)POTION).Should().BeTrue();
        plant.HasBred.Should().Be(bred);
    }

    private async Task<bool> BreedAsync()
    {
        var owner = ActionContext.CreateForPlayer(OWNER, 1);

        return await Pets.BreedPetsAsync(owner, PetBreedingAction.Request, PLANT, OTHER_PLANT, Ct)
            && await Pets.BreedPetsAsync(owner, PetBreedingAction.Accept, OTHER_PLANT, PLANT, Ct);
    }

    private Task<bool> UsePotionAsync(string extraData, int petId)
    {
        var potion = _room.CreateFloorItem(
            POTION,
            2,
            2,
            Altitude.Zero,
            name: "mnstr_rebreed",
            logic: "pet_rebreed",
            createLogic: (stuffDataFactory, ctx) =>
                new FurniturePetRebreedLogic(stuffDataFactory, ctx)
        );
        potion.SetExtraData(extraData);
        _room.AddToRoom(potion);

        return _room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            POTION,
            new UseWithPetInteraction { PetId = petId },
            Ct
        );
    }

    private static RoomHarness CreateRoom()
    {
        var room = new RoomHarness();

        // The room tells everyone in it that the furni is gone and the plants changed.
        var stream = typeof(RoomGrain).GetField(
            "_roomOutbound",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;
        stream.SetValue(room.Room, room.Fakes.Create(stream.FieldType, "room-stream"));

        room.Fakes.Handlers["TryGetDefinitionByName"] = call =>
            call.Args[0] is "mnstr_seed" ? Seed() : null;
        room.Fakes.Handlers[nameof(IInventoryGrain.GrantFurnitureAsync)] = _ =>
            Task.FromResult<FurnitureItemSnapshot?>(
                (FurnitureItemSnapshot)
                    RuntimeHelpers.GetUninitializedObject(typeof(FurnitureItemSnapshot))
            );

        return room;
    }

    private RoomPetAvatar AddPlant(int petId, int rarityLevel)
    {
        RoomObjectId objectId = 100 + petId;
        var plant = RoomPetAvatar.FromSnapshot(
            objectId,
            new PetSnapshot
            {
                Id = petId,
                OwnerId = OWNER,
                OwnerName = "owner",
                RoomId = null,
                Name = $"plant{petId}",
                Figure = new PetFigureSnapshot
                {
                    TypeId = PetTypes.MONSTERPLANT,
                    PaletteId = 1,
                    Color = "FFFFFF",
                    BreedId = 0,
                    CustomParts = [],
                },
                Level = 7,
                Experience = 0,
                Energy = 100,
                Nutrition = 100,
                Respect = 0,
                RarityLevel = rarityLevel,
                HasSaddle = false,
                AnyoneCanRide = false,
                HasBreedingPermission = false,
                X = petId - 39,
                Y = 1,
                Z = Altitude.Zero,
                Rotation = Rotation.South,
                CreatedAtUtc = DateTime.UtcNow,
                WateredAtUtc = DateTime.UtcNow,
                HarvestedAtUtc = null,
            }
        );

        (
            (IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(_room.State, "AvatarsByObjectId")!
        )[objectId] = plant;
        ((IDictionary<int, RoomObjectId>)RoomHarness.GetMember(_room.State, "AvatarsByPetId")!)[
            petId
        ] = objectId;
        ((IList<IRoomPet>)RoomHarness.GetMember(_room.State, "Pets")!).Add(plant);

        return plant;
    }

    private static FurnitureDefinitionSnapshot Seed() =>
        new()
        {
            Id = SEED_DEFINITION,
            SpriteId = SEED_DEFINITION,
            Name = "mnstr_seed",
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            LogicName = "default",
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
            CustomParams = null,
        };
}
