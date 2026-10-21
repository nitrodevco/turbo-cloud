using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;
using Turbo.Rooms.Providers;
using Turbo.Runtime.Configuration;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Breeding two pets in a nest gives the owner a baby pet, and the nest disappears in the process
/// (breedpets.confirmation.widget.text). The nest stayed in the room, so it bred again and again;
/// and the baby is written at once by the inventory, so the nest has to be gone from the database
/// before it, not on the room's next hand-over.
/// </summary>
public sealed class PetBreedingNestTests
{
    private const int OWNER = 1;
    private const int PET1 = 40;
    private const int PET2 = 41;
    private const int OFFSPRING = 99;
    private const int NEST = 70;

    private readonly WiredRoom _room = new(10, 10);
    private readonly RoomPlayerAvatar _owner = new() { ObjectId = 5, PlayerId = OWNER };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext Owner => ActionContext.CreateForPlayer(OWNER, 1);

    public PetBreedingNestTests()
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

        harness.Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutPetAsync)] = call =>
            Task.FromResult<PetSnapshot?>(Dog((int)call.Args[0]!));
        harness.Fakes.Handlers[nameof(IInventoryGrain.CreatePetAsync)] = _ =>
            Task.FromResult<PetSnapshot?>(Dog(OFFSPRING));
    }

    private RoomPetModule Pets => _room.Harness.Module<RoomPetModule>();

    [Fact]
    public async Task The_nest_is_used_up_and_written_before_the_baby_is_created()
    {
        await SitBothInNestAsync();

        (await Pets.ConfirmNestBreedingAsync(Owner, NEST, "Puppy", PET1, PET2, Ct))
            .Should()
            .BeTrue();

        _room
            .Harness.State.ItemsById.ContainsKey((RoomObjectId)NEST)
            .Should()
            .BeFalse("the nest disappears in breeding");

        var calls = _room.Harness.Fakes.Log.Calls.ToList();
        var used = calls.FindIndex(x =>
            x.Method == "WriteUseAsync"
            && x.Args[1] is UsedUpItem { Item.ObjectId: var id }
            && id == NEST
        );
        used.Should().BeGreaterThanOrEqualTo(0, "the nest's row is deleted at once");
        used.Should()
            .BeLessThan(calls.FindIndex(x => x.Method == nameof(IInventoryGrain.CreatePetAsync)));
    }

    [Fact]
    public async Task A_nest_picked_up_before_the_last_confirm_breeds_nothing()
    {
        await SitBothInNestAsync();
        _room.Harness.State.ItemsById.Remove((RoomObjectId)NEST);

        (await Pets.ConfirmNestBreedingAsync(Owner, NEST, "Puppy", PET1, PET2, Ct))
            .Should()
            .BeFalse();

        _room
            .Harness.Fakes.Log.Calls.Should()
            .NotContain(x => x.Method == nameof(IInventoryGrain.CreatePetAsync));
    }

    /// <summary>
    /// Every official nest is one tile (furnidata pet_breeding_* 1x1) and holds both parents
    /// ("the parent pets ran off the nest", breedpets.confirmation.alert.petsmissing.desc), so the
    /// second pet told to breed walks onto the tile the first lies on. It stopped short: the pet
    /// already there blocked the tile like any avatar.
    /// </summary>
    [Fact]
    public async Task The_second_pet_told_to_breed_walks_into_a_nest_the_first_lies_in()
    {
        var nest = AddNest();
        var first = await PlaceAsync(PET1, 6, 3);
        var second = await PlaceAsync(PET2, 3, 6);

        (await BreedAsync(first)).Should().BeTrue();
        await TickAsync(first);
        nest.OccupantPetIds.Should().Equal(PET1);
        _room.TileFlags()[6 * 10 + 6].Should().HaveFlag(RoomTileFlags.AvatarOccupied);

        (await BreedAsync(second)).Should().BeTrue();
        await TickAsync(second);

        (second.X, second.Y).Should().Be((6, 6));
        (first.X, first.Y).Should().Be((6, 6));
        first
            .HasStatus(AvatarStatusType.Lay)
            .Should()
            .BeTrue("the first parent stays lying when the second steps in");
        second.HasStatus(AvatarStatusType.Lay).Should().BeTrue();
        nest.OccupantPetIds.Should().Equal(PET1, PET2);
        _room
            .Harness.State.PendingNestBreedings.ContainsKey((RoomObjectId)NEST)
            .Should()
            .BeTrue("both parents are in the nest, so their owners are asked to confirm");
    }

    [Fact]
    public async Task The_second_pet_may_be_told_while_the_first_takes_its_last_step_in()
    {
        var nest = AddNest();
        var first = await PlaceAsync(PET1, 6, 3);
        var second = await PlaceAsync(PET2, 3, 6);

        (await BreedAsync(first)).Should().BeTrue();

        for (var step = 0; step < 20 && (first.X, first.Y) != (6, 6); step++)
        {
            _now += 10_000L;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
        }

        nest.OccupantPetIds.Should().BeEmpty("the first pet is on the tile, not yet in the nest");

        (await BreedAsync(second)).Should().BeTrue();
        await TickAsync(second);

        (second.X, second.Y).Should().Be((6, 6));
        nest.OccupantPetIds.Should().BeEquivalentTo([PET1, PET2]);
    }

    [Fact]
    public async Task A_player_on_the_nest_still_keeps_a_pet_out()
    {
        AddNest();
        _room.Map.RemoveAvatar(_owner, false);
        _owner.SetPosition(6, 6);
        _room.Map.AddAvatar(_owner, false);
        var pet = await PlaceAsync(PET2, 3, 6);

        (await BreedAsync(pet)).Should().BeFalse();
        (pet.X, pet.Y).Should().Be((3, 6));
    }

    /// <summary>
    /// A pet is created on the tile it is put down on, so its first step went nowhere and never
    /// took the tile: until it first moved, another pet could be put down on it and anyone could
    /// walk onto it.
    /// </summary>
    [Fact]
    public async Task A_pet_just_put_down_holds_its_tile_before_its_first_step()
    {
        await PlaceAsync(PET1, 3, 3);

        _room.TileFlags()[3 * 10 + 3].Should().HaveFlag(RoomTileFlags.AvatarOccupied);
        (await Pets.PlacePetAsync(Owner, PET2, 3, 3, Ct))
            .Should()
            .BeFalse("the tile is the first pet's");
        Pets.TryGetPet(PET2, out _).Should().BeFalse();
    }

    /// <summary>
    /// The confirm window numbers the categories in the order sent and always shows its four
    /// rows, EPIC first (ConfirmPetBreedingView.as:198-217, confirm_pet_breeding.xml breeds1-4,
    /// breedpets.confirmation.widget.raritycategory.1-4). Only the levels with palettes were sent,
    /// commonest first: every dog palette landed under EPIC at the commonest chance and the other
    /// rows showed "%percent%" unfilled.
    /// </summary>
    [Fact]
    public async Task The_confirm_window_gets_every_rarity_row_rarest_first()
    {
        _room.Harness.Room.UsePetBreeds(_room.Harness.Fakes.Create<IPetBreedProvider>());
        _room.Harness.Fakes.Handlers[nameof(IPetBreedProvider.GetPalettes)] = _ =>
            ImmutableArray.Create(Palette(0, 0), Palette(1, 0), Palette(7, 2));
        AddNest();
        var first = await PlaceAsync(PET1, 6, 3);
        var second = await PlaceAsync(PET2, 3, 6);

        (await BreedAsync(first)).Should().BeTrue();
        await TickAsync(first);
        (await BreedAsync(second)).Should().BeTrue();
        await TickAsync(second);

        var request = _room
            .Harness.Fakes.SentToPlayer<ConfirmBreedingRequestEventMessageComposer>(OWNER)
            .Single();

        request.RarityCategories.Select(x => x.Chance).Should().Equal(3, 7, 20, 70);
        request
            .RarityCategories.Select(x => x.PaletteIds.ToArray())
            .Should()
            .BeEquivalentTo(new int[][] { [], [7], [], [0, 1] }, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task The_confirm_window_shows_the_chances_the_configuration_gives_not_the_defaults()
    {
        // As the host binds Turbo:Pets from appsettings.json (or the panel): the chances given
        // are the chances, not four more after the class's own 70, 20, 7, 3.
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(
                new MemoryStream(
                    """{ "Turbo": { "Pets": { "BreedingRarityChances": [40, 30, 20, 10] } } }"""u8.ToArray()
                )
            )
            .Build();
        using var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddWholeCollectionBinding()
            .Configure<PetConfig>(configuration.GetSection(PetConfig.SECTION_NAME))
            .BuildServiceProvider();

        _room.Harness.Room.UsePetConfig(services.GetRequiredService<IOptions<PetConfig>>().Value);
        AddNest();
        var first = await PlaceAsync(PET1, 6, 3);
        var second = await PlaceAsync(PET2, 3, 6);

        (await BreedAsync(first)).Should().BeTrue();
        await TickAsync(first);
        (await BreedAsync(second)).Should().BeTrue();
        await TickAsync(second);

        var request = _room
            .Harness.Fakes.SentToPlayer<ConfirmBreedingRequestEventMessageComposer>(OWNER)
            .Single();

        // Rarest first: EPIC (level 3) .. COMMON (level 0).
        request.RarityCategories.Select(x => x.Chance).Should().Equal(10, 20, 30, 40);
    }

    private static PetBreedSnapshot Palette(int paletteId, int rarityLevel) =>
        new()
        {
            TypeId = PetTypes.DOG,
            BreedId = paletteId,
            PaletteId = paletteId,
            RarityLevel = rarityLevel,
            Sellable = true,
            Rare = false,
            ColorTag = -1,
        };

    private FurniturePetBreedingNestLogic AddNest() =>
        (FurniturePetBreedingNestLogic)
            _room
                .AddFloorItem(
                    NEST,
                    6,
                    6,
                    "pet_breeding_nest",
                    createLogic: (factory, ctx) => new FurniturePetBreedingNestLogic(factory, ctx)
                )
                .Logic;

    private async Task<IRoomPet> PlaceAsync(int petId, int x, int y)
    {
        (await Pets.PlacePetAsync(Owner, petId, x, y, Ct)).Should().BeTrue();
        Pets.TryGetPet(petId, out var pet).Should().BeTrue();

        return pet!;
    }

    /// <summary>The "breed" pet command, as the owner's chat gives it.</summary>
    private Task<bool> BreedAsync(IRoomPet pet) =>
        Pets.ExecuteCommandAsync(Owner, pet, PetCommandType.Breed, Ct);

    private async Task TickAsync(IRoomPet pet)
    {
        var room = _room.Harness.Room;

        for (var step = 0; step < 20 && (pet.IsWalking || pet.TargetItemId > 0); step++)
        {
            _now += 10_000L;
            await room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
            await room.PetTickSystem.ProcessPetsAsync(_now, Ct);
        }
    }

    private long _now;

    private async Task SitBothInNestAsync()
    {
        var nest = AddNest();

        foreach (var (petId, x) in new[] { (PET1, 4), (PET2, 5) })
        {
            (await Pets.PlacePetAsync(Owner, petId, x, 4, Ct)).Should().BeTrue();
            Pets.TryGetPet(petId, out var pet).Should().BeTrue();
            nest.AddOccupant(pet!).Should().BeTrue();
        }

        _room.Harness.State.PendingNestBreedings[(RoomObjectId)NEST] = new NestBreedingSession
        {
            NestId = NEST,
            Pet1Id = PET1,
            Pet2Id = PET2,
            OwnerIds = [OWNER],
        };
    }

    private static PetSnapshot Dog(int id) =>
        new()
        {
            Id = id,
            OwnerId = OWNER,
            OwnerName = "owner",
            RoomId = null,
            Name = "Rex",
            Figure = new PetFigureSnapshot
            {
                TypeId = PetTypes.DOG,
                PaletteId = 1,
                Color = "FFFFFF",
                BreedId = 0,
                CustomParts = [],
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
