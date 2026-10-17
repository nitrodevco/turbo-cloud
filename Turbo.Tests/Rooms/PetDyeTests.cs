using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;
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
/// Horse dyes, hair dyes and hair styles used on the owner's horse, carrying only what Habbo's furni
/// data gives them - their <c>customparams</c> (<c>horse_dye_08</c> "15 Gold",
/// <c>horse_hairdye_06</c> "15 2,3 54,54", <c>horse_hairstyle_3</c> "15 2,3 103,103 1,1"), no
/// extra data section: the horse changes and the item is used up. Before, every one was refused
/// ("the item carries no dye data") and the horse stayed as it was.
/// </summary>
public class PetDyeTests
{
    private const int ITEM = 7;
    private const int OWNER = 1;
    private const int PET_OBJECT = 40;
    private const int PET_ID = 940;

    /// <summary>Breed 1 horse palettes: 2 is Beige (15), 30 is Gold (18).</summary>
    private const int BEIGE_PALETTE = 2;
    private const int GOLD_PALETTE = 30;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_body_dye_moves_the_horse_to_its_breeds_palette_of_that_colour()
    {
        var (room, pet) = CreateRoom([]);

        (await UseAsync(room, "horse_dye_08", "pet_dye", "15 Gold", Dye)).Should().BeTrue();

        pet.PetFigure.PaletteId.Should().Be(GOLD_PALETTE);
        room.ItemsById.Contains((RoomObjectId)ITEM).Should().BeFalse();
    }

    [Fact]
    public async Task A_hair_dye_recolours_the_listed_layers_and_keeps_their_parts()
    {
        var (room, pet) = CreateRoom([2, 101, 1, 3, 101, 1]);

        (await UseAsync(room, "horse_hairdye_06", "pet_custom_part", "15 2,3 54,54", Part))
            .Should()
            .BeTrue();

        pet.PetFigure.CustomParts.Should().Equal(2, 101, 54, 3, 101, 54);
    }

    [Fact]
    public async Task A_hair_style_sets_the_listed_layers_and_keeps_their_colour()
    {
        var (room, pet) = CreateRoom([2, 101, 54]);

        (await UseAsync(room, "horse_hairstyle_3", "pet_custom_part", "15 2,3 103,103 1,1", Part))
            .Should()
            .BeTrue();

        pet.PetFigure.CustomParts.Should().Equal(2, 103, 54, 3, 103, 1);
    }

    [Fact]
    public async Task A_horse_product_is_refused_by_another_kind_of_pet()
    {
        var (room, pet) = CreateRoom([], PetTypes.HORSE + 1);

        (await UseAsync(room, "horse_hairdye_06", "pet_custom_part", "15 2,3 54,54", Part))
            .Should()
            .BeFalse();

        pet.PetFigure.CustomParts.Should().BeEmpty();
        room.ItemsById.Contains((RoomObjectId)ITEM).Should().BeTrue();
    }

    private static FurniturePetProductLogic Dye(
        Turbo.Primitives.Furniture.Providers.IStuffDataFactory stuffDataFactory,
        Turbo.Primitives.Rooms.Object.Furniture.Floor.IRoomFloorItemContext ctx
    ) => new FurniturePetDyeLogic(stuffDataFactory, ctx);

    private static FurniturePetProductLogic Part(
        Turbo.Primitives.Furniture.Providers.IStuffDataFactory stuffDataFactory,
        Turbo.Primitives.Rooms.Object.Furniture.Floor.IRoomFloorItemContext ctx
    ) => new FurniturePetCustomPartLogic(stuffDataFactory, ctx);

    private static Task<bool> UseAsync(
        RoomHarness room,
        string name,
        string logic,
        string customParams,
        Func<
            Turbo.Primitives.Furniture.Providers.IStuffDataFactory,
            Turbo.Primitives.Rooms.Object.Furniture.Floor.IRoomFloorItemContext,
            FurniturePetProductLogic
        > create
    )
    {
        room.AddToRoom(
            room.CreateFloorItem(
                ITEM,
                2,
                2,
                Altitude.Zero,
                name: name,
                logic: logic,
                createLogic: (stuffDataFactory, ctx) => create(stuffDataFactory, ctx),
                customParams: customParams
            )
        );

        return room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            ITEM,
            new UseWithPetInteraction { PetId = PET_ID },
            Ct
        );
    }

    private static (RoomHarness Room, IRoomPet Pet) CreateRoom(
        int[] customParts,
        int typeId = PetTypes.HORSE
    )
    {
        var room = new RoomHarness();

        // The room tells everyone in it that the furni is gone and the pet changed.
        var stream = typeof(RoomGrain).GetField(
            "_roomOutbound",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;
        stream.SetValue(room.Room, room.Fakes.Create(stream.FieldType, "room-stream"));

        RoomHarness.SetField(
            room.Room,
            "_petBreedProvider",
            room.Fakes.Create<IPetBreedProvider>()
        );
        room.Fakes.Handlers[nameof(IPetBreedProvider.GetPalettes)] = _ =>
            ImmutableArray.Create(Palette(BEIGE_PALETTE, 15), Palette(GOLD_PALETTE, 18));

        var pet = RoomPetAvatar.FromSnapshot(
            PET_OBJECT,
            new PetSnapshot
            {
                Id = PET_ID,
                OwnerId = OWNER,
                OwnerName = "owner",
                RoomId = null,
                Name = "pet",
                Figure = new PetFigureSnapshot
                {
                    TypeId = typeId,
                    PaletteId = BEIGE_PALETTE,
                    Color = "FFFFFF",
                    BreedId = 1,
                    CustomParts = [.. customParts],
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

        (
            (IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(room.State, "AvatarsByObjectId")!
        )[PET_OBJECT] = pet;
        ((IDictionary<int, RoomObjectId>)RoomHarness.GetMember(room.State, "AvatarsByPetId")!)[
            PET_ID
        ] = PET_OBJECT;
        ((IList<IRoomPet>)RoomHarness.GetMember(room.State, "Pets")!).Add(pet);

        return (room, pet);
    }

    private static PetBreedSnapshot Palette(int paletteId, int colorTag) =>
        new()
        {
            TypeId = PetTypes.HORSE,
            BreedId = 1,
            PaletteId = paletteId,
            RarityLevel = 0,
            Sellable = true,
            Rare = false,
            ColorTag = colorTag,
        };
}
