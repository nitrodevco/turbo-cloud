using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Harvesting a grown monsterplant gives its owner a seed. The seed is written at once by the
/// inventory; the plant's harvest time waited for the room's next hand-over, so a crash in
/// between kept the seed and left the plant harvestable again.
/// </summary>
public sealed class MonsterplantHarvestTests
{
    private const int OWNER = 1;
    private const int PLANT = 40;
    private const int SEED_DEFINITION = 4300;

    private readonly WiredRoom _room = new(10, 10);
    private readonly RoomPlayerAvatar _owner = new() { ObjectId = 5, PlayerId = OWNER };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public MonsterplantHarvestTests()
    {
        var harness = _room.Harness;

        _owner.SetPosition(3, 4);
        _owner.SetLogic(harness.Fakes.Create<IRoomPlayerLogic>(5));
        _room.Harness.State.AvatarsByObjectId[5] = _owner;
        _room.Harness.State.AvatarsByPlayerId[OWNER] = 5;
        _room.Map.AddAvatar(_owner, false);
        _room.Harness.State.RoomSnapshot = _room.Harness.State.RoomSnapshot with
        {
            AllowPets = true,
        };
        harness.Room.UseAvatarProvider(new RoomAvatarProvider(Options.Create(new BotConfig())));

        harness.Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutPetAsync)] = _ =>
            Task.FromResult<PetSnapshot?>(Plant());
        harness.Fakes.Handlers["TryGetDefinitionByName"] = call =>
            call.Args[0] is "mnstr_seed" ? Seed() : null;
    }

    private RoomPetModule Pets => _room.Harness.Module<RoomPetModule>();

    [Fact]
    public async Task The_harvested_plant_is_written_before_the_seed_is_given()
    {
        var owner = ActionContext.CreateForPlayer(OWNER, 1);
        (await Pets.PlacePetAsync(owner, PLANT, 4, 4, Ct)).Should().BeTrue();

        await Pets.HarvestPetAsync(owner, PLANT, Ct);

        var calls = _room.Harness.Fakes.Log.Calls.ToList();
        var harvested = calls.FindIndex(x =>
            x.Method == "WriteUseAsync"
            && x.Args[2] is List<PetSnapshot> pets
            && pets.Any(p => p.Id == PLANT && p.HarvestedAtUtc is not null)
        );
        harvested.Should().BeGreaterThanOrEqualTo(0, "the harvest is written at once");
        harvested
            .Should()
            .BeLessThan(
                calls.FindIndex(x => x.Method == nameof(IInventoryGrain.GrantFurnitureAsync))
            );
    }

    [Fact]
    public async Task A_planted_seed_is_used_up_before_its_plant_is_created()
    {
        const int SEED = 60;
        var seed = _room.AddFloorItem(
            SEED,
            3,
            3,
            "monsterplant_seed",
            createLogic: (factory, ctx) => new FurnitureMonsterplantSeedLogic(factory, ctx)
        );

        await seed.Logic.OnUseAsync(ActionContext.CreateForPlayer(OWNER, 1), 0, Ct);

        var calls = _room.Harness.Fakes.Log.Calls.ToList();
        var used = calls.FindIndex(x =>
            x.Method == "WriteUseAsync"
            && x.Args[1] is UsedUpItem { Item.ObjectId: var id }
            && id == SEED
        );
        used.Should().BeGreaterThanOrEqualTo(0);
        used.Should()
            .BeLessThan(calls.FindIndex(x => x.Method == nameof(IInventoryGrain.CreatePetAsync)));
        // The fake inventory created no plant: the seed goes back to its owner.
        calls.Should().Contain(x => x.Method == nameof(IInventoryGrain.GrantFurnitureAsync));
    }

    [Fact]
    public async Task Planting_a_seed_and_treating_the_plant_count_for_the_reward_tracks()
    {
        // Habboween 2026: "Plant a monsterplant seed" (plant_mnstr, PLANT_MONSTER_PLANT) and
        // "Treat a monsterplant" (treat_mnstr, TREAT_MONSTER_PLANT). The plant's owner is not
        // the room's, but a plant is no pet taken visiting.
        _room.Harness.State.RoomSnapshot = _room.Harness.State.RoomSnapshot with
        {
            OwnerId = (PlayerId)77,
        };
        _room.Harness.Fakes.Handlers[nameof(IInventoryGrain.CreatePetAsync)] = _ =>
            Task.FromResult<PetSnapshot?>(Plant());
        _room.Harness.Fakes.Handlers["ApplyPetRespectOperationAsync"] = _ =>
            Task.FromResult(new PetRespectOperationResult { Accepted = true, Respect = 1 });
        var seed = _room.AddFloorItem(
            60,
            3,
            3,
            "monsterplant_seed",
            createLogic: (factory, ctx) => new FurnitureMonsterplantSeedLogic(factory, ctx)
        );
        var owner = ActionContext.CreateForPlayer(OWNER, 1);

        await seed.Logic.OnUseAsync(owner, 0, Ct);
        (await Pets.RespectPetAsync(owner, PLANT, Ct)).Should().BeTrue();

        _room
            .Harness.Fakes.Log.Of("RecordActionAsync")
            .Select(x => x.Args[0])
            .Should()
            .Equal(
                RewardTrackActionTypes.PLANT_MONSTER_PLANT,
                RewardTrackActionTypes.TREAT_MONSTER_PLANT
            );
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

    private static PetSnapshot Plant() =>
        new()
        {
            Id = PLANT,
            OwnerId = OWNER,
            OwnerName = "owner",
            RoomId = null,
            Name = "Sprout",
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
