using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A pet product used straight from the inventory. Flash's use button (<c>FurniView</c>
/// <c>use_btn</c>, offered for pet shampoo, custom parts, saddles and the revival potion) sends
/// <c>CustomizePetWithFurni</c> with the item's inventory ref, which for a floor item is its id
/// negated, where the room's own flow sends the room object id. Before, the server dropped every
/// id that was not positive, so the use button did nothing. Now the product follows the same
/// rules as one standing in the room, is used up from the inventory when it takes effect, and
/// goes back to the inventory when it then has none.
/// </summary>
public sealed class PetProductFromInventoryTests
{
    private const int OWNER = 1;
    private const int OTHER = 2;
    private const int PET = 40;
    private const int ITEM = 77;
    private const int SADDLE_DEFINITION = 4450;
    private const int DYE_DEFINITION = 5001;
    private const string SADDLE = "15 4 10 0 103";

    private readonly WiredRoom _room = new(10, 10);
    private readonly RoomPlayerAvatar _owner = new() { ObjectId = 5, PlayerId = OWNER };

    /// <summary>What the inventory holds under <see cref="ITEM"/>; null when it holds nothing.</summary>
    private FurnitureItemSnapshot? _held;

    /// <summary>What the inventory gives up when asked; by default what it holds.</summary>
    private Func<FurnitureItemSnapshot?>? _consume;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PetProductFromInventoryTests()
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
            Task.FromResult<PetSnapshot?>(Horse());
        harness.Fakes.Handlers[nameof(IInventoryGrain.GetItemSnapshotAsync)] = _ =>
            Task.FromResult(_held);
        harness.Fakes.Handlers[nameof(IInventoryGrain.ConsumeFurnitureAsync)] = _ =>
            Task.FromResult(_consume is null ? _held : _consume());
        // The room builds the item it is given the way it builds one being placed.
        harness.Fakes.Handlers["CreateFromFurnitureItemSnapshot"] = call =>
        {
            var snapshot = (FurnitureItemSnapshot)call.Args[0]!;
            var item = new RoomFloorItem
            {
                ObjectId = snapshot.ItemId,
                OwnerId = snapshot.OwnerId,
                OwnerName = snapshot.OwnerName,
                Definition = snapshot.Definition,
            };

            item.SetExtraData(snapshot.ExtraData);

            return item;
        };
        // Breed 0 horse palettes: only beige (15), so a gold dye finds no palette.
        harness.Fakes.Handlers[nameof(IPetBreedProvider.GetPalettes)] = _ =>
            ImmutableArray.Create(
                new PetBreedSnapshot
                {
                    TypeId = PetTypes.HORSE,
                    BreedId = 0,
                    PaletteId = 3,
                    RarityLevel = 0,
                    Sellable = true,
                    Rare = false,
                    ColorTag = 15,
                }
            );
    }

    private object Member(string name) => RoomHarness.GetMember(_room.Harness.State, name)!;

    private RoomPetModule Pets => _room.Harness.Module<RoomPetModule>();

    private Fakes Fakes => _room.Harness.Fakes;

    [Fact]
    public async Task A_saddle_from_the_inventory_is_strapped_on_and_used_up_from_the_inventory()
    {
        await PlaceAsync();
        Holds(Saddle());

        (await UseAsync(OWNER)).Should().BeTrue();

        Pet().HasSaddle.Should().BeTrue();
        var consume = Fakes.Log.Of(nameof(IInventoryGrain.ConsumeFurnitureAsync)).Single();
        consume.Key.Should().Be((long)OWNER);
        consume.Args[0].Should().Be((RoomObjectId)ITEM);
        ((MovementCause)consume.Args[1]!).Cause.Should().Be(MovementCauses.USE_PET_PRODUCT);
        Fakes.Log.Of(nameof(IInventoryGrain.GrantFurnitureAsync)).Should().BeEmpty();
        // The saddled horse is written at once, as a use in the room writes it with the item.
        Fakes
            .Log.Of("WriteUseAsync")
            .Should()
            .Contain(x =>
                x.Args[1] == null
                && ((List<PetSnapshot>)x.Args[2]!).Any(p =>
                    p.Id == PET && p.HasSaddle && p.SaddleDefinitionId == SADDLE_DEFINITION
                )
            );
    }

    [Fact]
    public async Task A_product_the_pet_does_not_take_stays_in_the_inventory()
    {
        await PlaceAsync(Horse() with { HasSaddle = true });
        Holds(Saddle());

        (await UseAsync(OWNER)).Should().BeFalse();

        NothingConsumed();
    }

    [Fact]
    public async Task A_product_is_not_used_on_another_players_pet()
    {
        await PlaceAsync();
        Holds(Saddle(owner: OTHER));

        (await UseAsync(OTHER)).Should().BeFalse();

        Pet().HasSaddle.Should().BeFalse();
        NothingConsumed();
    }

    [Fact]
    public async Task A_product_is_not_used_on_a_pet_that_is_not_in_the_room()
    {
        Holds(Saddle());

        (await UseAsync(OWNER)).Should().BeFalse();

        NothingConsumed();
    }

    [Fact]
    public async Task A_rented_product_is_never_used_up()
    {
        await PlaceAsync();
        Holds(Saddle() with { SecondsToExpiration = 3600 });

        (await UseAsync(OWNER)).Should().BeFalse();

        Pet().HasSaddle.Should().BeFalse();
        NothingConsumed();
    }

    [Fact]
    public async Task Furni_that_is_no_pet_product_is_not_used_up()
    {
        await PlaceAsync();
        Holds(
            Saddle() with
            {
                Definition = Saddle().Definition with { LogicName = "default_floor" },
            }
        );

        (await UseAsync(OWNER)).Should().BeFalse();

        NothingConsumed();
    }

    [Fact]
    public async Task An_item_the_inventory_no_longer_holds_changes_no_pet()
    {
        await PlaceAsync();
        Holds(Saddle());
        _consume = () => null;

        (await UseAsync(OWNER)).Should().BeFalse();

        Pet().HasSaddle.Should().BeFalse();
        Fakes.Log.Of(nameof(IInventoryGrain.GrantFurnitureAsync)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_product_that_then_has_no_effect_is_given_back_to_the_inventory()
    {
        await PlaceAsync();
        Holds(Dye());

        (await UseAsync(OWNER)).Should().BeFalse();

        Pet().PetFigure.PaletteId.Should().Be(3);
        Fakes.Log.Of(nameof(IInventoryGrain.ConsumeFurnitureAsync)).Should().ContainSingle();
        GivenBack().Should().Be((DYE_DEFINITION, Dye().ExtraData));
    }

    [Fact]
    public async Task A_product_that_fails_while_applied_is_given_back_to_the_inventory()
    {
        await PlaceAsync();
        Holds(Dye());
        Fakes.Handlers[nameof(IPetBreedProvider.GetPalettes)] = _ =>
            throw new InvalidOperationException("breeds unavailable");

        (await UseAsync(OWNER)).Should().BeFalse();

        GivenBack().Should().Be((DYE_DEFINITION, Dye().ExtraData));
    }

    [Fact]
    public async Task A_negative_id_is_the_inventory_item_of_that_id()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("CustomizePetWithFurniMessageEvent"),
            PacketHarness.Payload(w => w.Int(-ITEM).Int(PET)),
            playerId: OWNER,
            roomId: 5
        );

        var call = harness
            .Fakes.Log.Of("UsePetProductFromInventoryAsync")
            .Should()
            .ContainSingle()
            .Subject;
        call.Key.Should().Be(5L);
        ((ActionContext)call.Args[0]!).PlayerId.Should().Be((PlayerId)OWNER);
        call.Args[1].Should().Be((RoomObjectId)ITEM);
        call.Args[2].Should().Be(PET);
        harness.Fakes.Log.Of("InteractWithItemAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task A_positive_id_is_still_the_product_standing_in_the_room()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("CustomizePetWithFurniMessageEvent"),
            PacketHarness.Payload(w => w.Int(ITEM).Int(PET)),
            playerId: OWNER,
            roomId: 5
        );

        harness
            .Fakes.Log.Of("InteractWithItemAsync")
            .Should()
            .ContainSingle()
            .Which.Args[1]
            .Should()
            .Be((RoomObjectId)ITEM);
        harness.Fakes.Log.Of("UsePetProductFromInventoryAsync").Should().BeEmpty();
    }

    private void Holds(FurnitureItemSnapshot item) => _held = item;

    private Task<bool> UseAsync(int playerId) =>
        _room.Harness.Room.UsePetProductFromInventoryAsync(
            ActionContext.CreateForPlayer(playerId, 1),
            ITEM,
            PET,
            Ct
        );

    private void NothingConsumed()
    {
        Fakes.Log.Of(nameof(IInventoryGrain.ConsumeFurnitureAsync)).Should().BeEmpty();
        Fakes.Log.Of(nameof(IInventoryGrain.GrantFurnitureAsync)).Should().BeEmpty();
    }

    private (int DefinitionId, string? ExtraData) GivenBack()
    {
        var grant = Fakes
            .Log.Of(nameof(IInventoryGrain.GrantFurnitureAsync))
            .Should()
            .ContainSingle()
            .Subject;

        return ((int)grant.Args[0]!, (string?)grant.Args[1]);
    }

    private async Task PlaceAsync(PetSnapshot? pet = null)
    {
        if (pet is not null)
            Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutPetAsync)] = _ =>
                Task.FromResult<PetSnapshot?>(pet);

        (await Pets.PlacePetAsync(ActionContext.CreateForPlayer(OWNER, 1), PET, 4, 4, Ct))
            .Should()
            .BeTrue();
    }

    private IRoomPet Pet()
    {
        Pets.TryGetPet(PET, out var pet).Should().BeTrue();

        return pet!;
    }

    private static FurnitureItemSnapshot Saddle(int owner = OWNER) =>
        Item(owner, SADDLE_DEFINITION, "horse_saddle2", "pet_saddle", SADDLE);

    private static FurnitureItemSnapshot Dye() =>
        Item(OWNER, DYE_DEFINITION, "horse_dye_08", "pet_dye", "15 Gold");

    private static FurnitureItemSnapshot Item(
        int owner,
        int definitionId,
        string name,
        string logic,
        string customParams
    ) =>
        new()
        {
            ItemId = ITEM,
            SpriteId = definitionId,
            OwnerId = owner,
            OwnerName = "holder",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = definitionId,
                SpriteId = definitionId,
                Name = name,
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.PetSaddle,
                LogicName = logic,
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
            },
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "0" },
            ExtraData = "{}",
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = -1,
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
