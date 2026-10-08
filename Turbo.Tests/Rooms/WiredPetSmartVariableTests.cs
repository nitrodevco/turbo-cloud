using System.Collections;
using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Avatars.Pet;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;
using Turbo.Rooms.Wired.Variables;
using Turbo.Rooms.Wired.Variables.User.Smart;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The pet, horse and monsterplant smart variables (Wired Faculty, 03/2026): <c>~pet.*</c> on
/// every pet but a monsterplant, <c>~horse.*</c> on horses, <c>~plant.*</c> on monsterplants,
/// each listed only while such a pet is in the room. A plant's shape and colour are the body
/// part and palette the monsterplant asset draws it with (body parts 1 to 12, palettes 0 to 10).
/// </summary>
public sealed class WiredPetSmartVariableTests
{
    private const int DOG = 40;
    private const int HORSE = 41;
    private const int PLANT = 42;

    private static readonly DateTime Created = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pet_variables_are_listed_only_while_a_pet_of_their_kind_is_in_the_room()
    {
        var level = Register(new PetLevelVariable(_room.Harness.Room));
        var saddle = Register(new HorseHasSaddleVariable(_room.Harness.Room));
        var wellbeing = Register(new PlantRemainingWellbeingSecondsVariable(_room.Harness.Room));

        (await Listed()).Should().NotContain([level, saddle, wellbeing]);

        AddPet(DOG, PetTypes.DOG);

        (await Listed()).Should().Contain(level).And.NotContain([saddle, wellbeing]);

        AddPet(HORSE, PetTypes.HORSE);
        AddPet(PLANT, PetTypes.MONSTERPLANT);

        (await Listed()).Should().Contain([level, saddle, wellbeing]);
    }

    [Fact]
    public void Pet_variables_read_the_stats_its_info_stand_shows()
    {
        AddPet(DOG, PetTypes.DOG);

        Read(new PetLevelVariable(_room.Harness.Room), DOG).Should().Be(4);
        Read(new PetExperienceVariable(_room.Harness.Room), DOG).Should().Be(450);
        Read(new PetEnergyVariable(_room.Harness.Room), DOG).Should().Be(60);
        Read(new PetHappinessVariable(_room.Harness.Room), DOG).Should().Be(70);
        Read(new PetScratchesVariable(_room.Harness.Room), DOG).Should().Be(9);
        Read(new PetOwnerIdVariable(_room.Harness.Room), DOG).Should().Be(77);
        Read(new PetMaxLevelVariable(_room.Harness.Room), DOG).Should().Be(20);
        Read(new PetExperienceRequiredVariable(_room.Harness.Room), DOG).Should().Be(600);
        Read(new PetMaxEnergyVariable(_room.Harness.Room), DOG).Should().Be(100);
        Read(new PetMaxHappinessVariable(_room.Harness.Room), DOG).Should().Be(100);
        Read(new PetCreationTimeVariable(_room.Harness.Room), DOG)
            .Should()
            .Be(new DateTimeOffset(Created).ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Kind_variables_are_not_held_by_the_other_kinds()
    {
        AddPet(DOG, PetTypes.DOG);
        AddPet(PLANT, PetTypes.MONSTERPLANT);
        var saddle = new HorseHasSaddleVariable(_room.Harness.Room);
        var level = new PetLevelVariable(_room.Harness.Room);
        var dead = new PlantIsDeadVariable(_room.Harness.Room);

        saddle.TryGetValue(Key(saddle, DOG), out _).Should().BeFalse();
        level.TryGetValue(Key(level, PLANT), out _).Should().BeFalse();
        dead.TryGetValue(Key(dead, DOG), out _).Should().BeFalse();
    }

    [Fact]
    public void Horse_variables_follow_its_saddle_and_rider()
    {
        var horse = AddPet(HORSE, PetTypes.HORSE);
        var rider = _room.Enter(5, 2, 2);
        var controller = new HorseControllerUserIdVariable(_room.Harness.Room);
        var riding = new HorseIsRidingVariable(_room.Harness.Room);

        Read(new HorseHasSaddleVariable(_room.Harness.Room), HORSE).Should().Be(1);
        Read(riding, HORSE).Should().Be(0);
        Read(controller, HORSE).Should().Be(0);

        horse.SetRider(rider.ObjectId);

        Read(riding, HORSE).Should().Be(1);
        Read(controller, HORSE).Should().Be(rider.PlayerId.Value);
    }

    [Fact]
    public void Plant_variables_count_down_its_wellbeing_and_growth()
    {
        var plant = AddPet(PLANT, PetTypes.MONSTERPLANT, watered: DateTime.UtcNow.AddHours(-1));

        Read(new PlantRarityLevelVariable(_room.Harness.Room), PLANT).Should().Be(2);
        Read(new PlantMaxWellbeingSecondsVariable(_room.Harness.Room), PLANT).Should().Be(259200);
        Read(new PlantRemainingWellbeingSecondsVariable(_room.Harness.Room), PLANT)
            .Should()
            .BeInRange(259200 - 3605, 259200 - 3595);
        Read(new PlantIsDeadVariable(_room.Harness.Room), PLANT).Should().Be(0);
        Read(new PlantIsGrowingVariable(_room.Harness.Room), PLANT).Should().Be(0);
        Read(new PlantRemainingGrowingSecondsVariable(_room.Harness.Room), PLANT).Should().Be(0);

        plant.SetWateredAt(DateTime.UtcNow.AddDays(-4));
        plant.SetFlags(canBreed: false, canHarvest: false, canRevive: true);

        Read(new PlantIsDeadVariable(_room.Harness.Room), PLANT).Should().Be(1);
        Read(new PlantRemainingWellbeingSecondsVariable(_room.Harness.Room), PLANT).Should().Be(0);
        Read(new PlantCanReviveVariable(_room.Harness.Room), PLANT).Should().Be(1);
        Read(new PlantCanBreedVariable(_room.Harness.Room), PLANT).Should().Be(0);
        Read(new PlantCanHarvestVariable(_room.Harness.Room), PLANT).Should().Be(0);
    }

    [Fact]
    public void A_young_plant_is_growing_until_its_next_level()
    {
        AddPet(PLANT, PetTypes.MONSTERPLANT, level: 1, created: DateTime.UtcNow.AddMinutes(-10));

        Read(new PlantIsGrowingVariable(_room.Harness.Room), PLANT).Should().Be(1);
        // A baby plant's wellbeing is 36 hours, a grown one's 72.
        Read(new PlantMaxWellbeingSecondsVariable(_room.Harness.Room), PLANT).Should().Be(129600);
        Read(new PlantRemainingWellbeingSecondsVariable(_room.Harness.Room), PLANT)
            .Should()
            .BeInRange(129600 - 5, 129600);
        Read(new PlantRemainingGrowingSecondsVariable(_room.Harness.Room), PLANT)
            .Should()
            .BeInRange(3000 - 5, 3000 + 5);
    }

    [Fact]
    public void Plant_shape_and_colour_are_its_body_part_and_palette()
    {
        AddPet(PLANT, PetTypes.MONSTERPLANT, paletteId: 3, customParts: [1, 7, 4]);
        AddPet(PLANT + 1, PetTypes.MONSTERPLANT, paletteId: 5);

        Read(new PlantShapeVariable(_room.Harness.Room), PLANT).Should().Be(7);
        Read(new PlantColorVariable(_room.Harness.Room), PLANT).Should().Be(4);
        // A plant drawn with the default body has no shape; its colour is the figure's palette.
        Read(new PlantShapeVariable(_room.Harness.Room), PLANT + 1).Should().Be(0);
        Read(new PlantColorVariable(_room.Harness.Room), PLANT + 1).Should().Be(5);
    }

    [Fact]
    public async Task A_planted_seed_grows_into_a_plant_of_one_of_the_twelve_shapes()
    {
        var seed = _room.AddFloorItem(
            60,
            3,
            3,
            "mnstr_seed",
            createLogic: (factory, ctx) => new FurnitureMonsterplantSeedLogic(factory, ctx)
        );
        var shapes = new HashSet<int>();
        ImmutableArray<PetBreedSnapshot> palettes =
        [
            .. Enumerable
                .Range(1, 10)
                .Select(id => new PetBreedSnapshot
                {
                    TypeId = PetTypes.MONSTERPLANT,
                    BreedId = id,
                    PaletteId = id,
                    RarityLevel = 0,
                    Sellable = false,
                    Rare = false,
                    ColorTag = -1,
                }),
        ];

        _room.Harness.Fakes.Handlers["GetPalettes"] = _ => palettes;
        _room.Harness.Fakes.Handlers["TryGetPalette"] = call =>
            palettes.FirstOrDefault(x => x.PaletteId == (int)call.Args[1]!);

        _room.Harness.Fakes.Handlers["CreatePetAsync"] = call =>
        {
            var parts = (ImmutableArray<int>)call.Args[6]!;

            parts.Length.Should().Be(3);
            parts[0].Should().Be(MonsterplantFigure.BODY_LAYER);
            parts[2].Should().Be((int)call.Args[2]!);
            shapes.Add(parts[1]);

            return Task.FromResult<PetSnapshot?>(null);
        };

        for (var i = 0; i < 300; i++)
            await seed.Logic.OnUseAsync(
                ActionContext.CreateForPlayer((PlayerId)77, (RoomId)1),
                0,
                Ct
            );

        shapes.Should().BeEquivalentTo(Enumerable.Range(1, 12));
    }

    private RoomPetAvatar AddPet(
        int objectId,
        int typeId,
        int? level = null,
        DateTime? created = null,
        DateTime? watered = null,
        int paletteId = 0,
        ImmutableArray<int> customParts = default
    )
    {
        var isPlant = PetTypes.IsMonsterplant(typeId);
        var pet = RoomPetAvatar.FromSnapshot(
            objectId,
            new PetSnapshot
            {
                Id = 900 + objectId,
                OwnerId = 77,
                OwnerName = "owner",
                RoomId = null,
                Name = "pet",
                Figure = new PetFigureSnapshot
                {
                    TypeId = typeId,
                    PaletteId = paletteId,
                    Color = "FFFFFF",
                    BreedId = 0,
                    CustomParts = customParts.IsDefault ? [] : customParts,
                },
                Level = level ?? (isPlant ? 7 : 4),
                Experience = 450,
                Energy = 60,
                Nutrition = 70,
                Respect = 9,
                RarityLevel = 2,
                HasSaddle = typeId == PetTypes.HORSE,
                AnyoneCanRide = false,
                HasBreedingPermission = false,
                X = 1,
                Y = 1,
                Z = 0,
                Rotation = 0,
                CreatedAtUtc = created ?? Created,
                WateredAtUtc = watered ?? DateTime.UtcNow,
                HarvestedAtUtc = null,
            }
        );

        (
            (IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(_room.Harness.State, "AvatarsByObjectId")!
        )[objectId] = pet;
        ((IList<IRoomPet>)RoomHarness.GetMember(_room.Harness.State, "Pets")!).Add(pet);

        return pet;
    }

    private WiredVariableId Register(WiredInternalVariable variable)
    {
        var id = variable.GetVarSnapshot().VariableId;
        var byId = (IDictionary)
            RoomHarness.GetMember(_room.Harness.Room.WiredSystem, "_variableById")!;

        byId[id] = variable;
        variable.GetVarSnapshot().VariableType.Should().Be(WiredVariableType.Smart);

        return id;
    }

    private async Task<List<WiredVariableId>> Listed() =>
        [
            .. (
                await _room.Harness.Room.WiredSystem.GetWiredVariablesSnapshotAsync(Ct)
            ).Variables.Select(x => x.VariableId),
        ];

    private static long Read(WiredInternalVariable variable, int objectId)
    {
        variable.TryGetValue(Key(variable, objectId), out var value).Should().BeTrue();

        return value;
    }

    private static WiredVariableKey Key(WiredInternalVariable variable, int objectId) =>
        new(variable.GetVarSnapshot().VariableId, WiredVariableTargetType.User, objectId);
}
