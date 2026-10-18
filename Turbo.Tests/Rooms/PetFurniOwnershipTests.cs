using System.Text.Json;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Avatars.Pet;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Pet packages and pet products turn into something their owner keeps (a pet, a change to a
/// pet), so only furni that is theirs for good may: a borrowed furni carries the borrower's id
/// and a copy placed by Place Temporary Furni the wired box owner's, and neither is worth
/// anything.
/// </summary>
public class PetFurniOwnershipTests
{
    private const int ITEM = 7;
    private const int OWNER = 1;
    private const int PET_OBJECT = 40;
    private const int PET_ID = 940;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(ITEM, true)]
    [InlineData(FurniIdBands.TemporaryMin, false)]
    [InlineData(FurniIdBands.BuildersClubMin, false)]
    public async Task A_pet_package_becomes_a_pet_only_when_it_is_its_owners_for_good(
        int id,
        bool opens
    )
    {
        var room = CreateRoom();
        RoomHarness.SetField(room.Room, "_petConfig", new PetConfig());
        RoomHarness.SetField(room.Room, "_wordFilter", room.Fakes.Create<IWordFilter>());
        RoomHarness.SetField(
            room.Room,
            "_petBreedProvider",
            room.Fakes.Create<IPetBreedProvider>()
        );
        room.Fakes.Handlers[nameof(IWordFilter.IsClean)] = _ => true;

        var package = room.CreateFloorItem(
            id,
            2,
            2,
            Altitude.Zero,
            name: "petbox_epic",
            logic: "pet_package",
            createLogic: (stuffDataFactory, ctx) =>
                new FurniturePetPackageLogic(stuffDataFactory, ctx)
        );
        package.SetExtraData(
            JsonSerializer.Serialize(
                new Dictionary<string, object>
                {
                    [PetPackageData.SECTION] = new PetPackageData
                    {
                        TypeId = 0,
                        PaletteId = 0,
                        Color = "FFFFFF",
                    },
                }
            )
        );
        room.AddToRoom(package);

        await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            id,
            new OpenPetPackageInteraction { Name = "Rex" },
            Ct
        );

        room.Fakes.Log.Of("CreatePetAsync").Should().HaveCount(opens ? 1 : 0);
        // The package goes before the pet is created; the inventory refused the pet here (the
        // fake creates none), so the package was given back to its owner's inventory.
        room.ItemsById.Contains((RoomObjectId)id).Should().Be(!opens);
        room.Fakes.Log.Of("GrantFurnitureAsync").Should().HaveCount(opens ? 1 : 0);
    }

    [Fact]
    public async Task A_pet_package_is_used_up_before_its_pet_is_created()
    {
        var room = CreateRoom();
        RoomHarness.SetField(room.Room, "_petConfig", new PetConfig());
        RoomHarness.SetField(room.Room, "_wordFilter", room.Fakes.Create<IWordFilter>());
        RoomHarness.SetField(
            room.Room,
            "_petBreedProvider",
            room.Fakes.Create<IPetBreedProvider>()
        );
        room.Fakes.Handlers[nameof(IWordFilter.IsClean)] = _ => true;

        var package = room.CreateFloorItem(
            ITEM,
            2,
            2,
            Altitude.Zero,
            name: "petbox_epic",
            logic: "pet_package",
            createLogic: (stuffDataFactory, ctx) =>
                new FurniturePetPackageLogic(stuffDataFactory, ctx)
        );
        package.SetExtraData(
            JsonSerializer.Serialize(
                new Dictionary<string, object>
                {
                    [PetPackageData.SECTION] = new PetPackageData
                    {
                        TypeId = 0,
                        PaletteId = 0,
                        Color = "FFFFFF",
                    },
                }
            )
        );
        room.AddToRoom(package);

        await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            ITEM,
            new OpenPetPackageInteraction { Name = "Rex" },
            Ct
        );

        var calls = room.Fakes.Log.Calls.ToList();
        var used = calls.FindIndex(x =>
            x.Method == "WriteUseAsync"
            && x.Args[1] is UsedUpItem { Item.ObjectId: var id }
            && id == ITEM
        );
        used.Should().BeGreaterThanOrEqualTo(0);
        used.Should().BeLessThan(calls.FindIndex(x => x.Method == "CreatePetAsync"));
    }

    [Theory]
    [InlineData(ITEM, true)]
    [InlineData(FurniIdBands.TemporaryMin, false)]
    [InlineData(FurniIdBands.BuildersClubMin, false)]
    public async Task A_pet_product_is_used_up_on_a_pet_only_when_it_is_its_owners_for_good(
        int id,
        bool used
    )
    {
        var room = CreateRoom();
        AddPet(room);
        var product = new CountingProduct.Box();

        room.AddToRoom(
            room.CreateFloorItem(
                id,
                2,
                2,
                Altitude.Zero,
                name: "horse_saddle1",
                logic: "pet_saddle",
                createLogic: (stuffDataFactory, ctx) =>
                    new CountingProduct(stuffDataFactory, ctx, product)
            )
        );

        var done = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            id,
            new UseWithPetInteraction { PetId = PET_ID },
            Ct
        );

        done.Should().Be(used);
        product.Applied.Should().Be(used ? 1 : 0);
        room.ItemsById.Contains((RoomObjectId)id).Should().Be(!used);
    }

    private static RoomHarness CreateRoom()
    {
        var room = new RoomHarness();

        // The room tells everyone in it that the furni is gone.
        var stream = typeof(RoomGrain).GetField(
            "_roomOutbound",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;
        stream.SetValue(room.Room, room.Fakes.Create(stream.FieldType, "room-stream"));

        return room;
    }

    private static void AddPet(RoomHarness room)
    {
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

        (
            (IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(room.State, "AvatarsByObjectId")!
        )[PET_OBJECT] = pet;
        ((IDictionary<int, RoomObjectId>)RoomHarness.GetMember(room.State, "AvatarsByPetId")!)[
            PET_ID
        ] = PET_OBJECT;
        ((IList<IRoomPet>)RoomHarness.GetMember(room.State, "Pets")!).Add(pet);
    }

    /// <summary>A product any pet takes, counting how often it was applied.</summary>
    private sealed class CountingProduct(
        IStuffDataFactory stuffDataFactory,
        IRoomFloorItemContext ctx,
        CountingProduct.Box box
    ) : FurniturePetProductLogic(stuffDataFactory, ctx)
    {
        public sealed class Box
        {
            public int Applied { get; set; }
        }

        protected override string? GetRefusal(IRoomPet pet) => null;

        protected override Task<bool> ApplyAsync(IRoomPet pet, CancellationToken ct)
        {
            box.Applied++;

            return Task.FromResult(true);
        }
    }
}
