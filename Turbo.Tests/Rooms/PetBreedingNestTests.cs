using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;
using Turbo.Rooms.Providers;
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
        ((IDictionary<RoomObjectId, IRoomAvatar>)Member("AvatarsByObjectId"))[5] = _owner;
        ((IDictionary<PlayerId, RoomObjectId>)Member("AvatarsByPlayerId"))[OWNER] = 5;
        _room.Map.AddAvatar(_owner, false);
        RoomHarness.SetMember(Member("RoomSnapshot"), "AllowPets", true);
        RoomHarness.SetField(
            harness.Room,
            "_avatarProvider",
            new RoomAvatarProvider(Options.Create(new BotConfig()))
        );

        harness.Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutPetAsync)] = call =>
            Task.FromResult<PetSnapshot?>(Dog((int)call.Args[0]!));
        harness.Fakes.Handlers[nameof(IInventoryGrain.CreatePetAsync)] = _ =>
            Task.FromResult<PetSnapshot?>(Dog(OFFSPRING));
    }

    private object Member(string name) => RoomHarness.GetMember(_room.Harness.State, name)!;

    private RoomPetModule Pets => _room.Harness.Module<RoomPetModule>();

    [Fact]
    public async Task The_nest_is_used_up_and_written_before_the_baby_is_created()
    {
        await SitBothInNestAsync();

        (await Pets.ConfirmNestBreedingAsync(Owner, NEST, "Puppy", PET1, PET2, Ct))
            .Should()
            .BeTrue();

        ((System.Collections.IDictionary)Member("ItemsById"))
            .Contains((RoomObjectId)NEST)
            .Should()
            .BeFalse("the nest disappears in breeding");

        var calls = _room.Harness.Fakes.Log.Calls.ToList();
        var used = calls.FindIndex(x =>
            x.Method == "WriteUseAsync" && x.Args[1] is RoomObjectId id && id == NEST
        );
        used.Should().BeGreaterThanOrEqualTo(0, "the nest's row is deleted at once");
        used.Should()
            .BeLessThan(calls.FindIndex(x => x.Method == nameof(IInventoryGrain.CreatePetAsync)));
    }

    [Fact]
    public async Task A_nest_picked_up_before_the_last_confirm_breeds_nothing()
    {
        await SitBothInNestAsync();
        ((System.Collections.IDictionary)Member("ItemsById")).Remove((RoomObjectId)NEST);

        (await Pets.ConfirmNestBreedingAsync(Owner, NEST, "Puppy", PET1, PET2, Ct))
            .Should()
            .BeFalse();

        _room
            .Harness.Fakes.Log.Calls.Should()
            .NotContain(x => x.Method == nameof(IInventoryGrain.CreatePetAsync));
    }

    private async Task SitBothInNestAsync()
    {
        var nest = (FurniturePetBreedingNestLogic)
            _room
                .AddFloorItem(
                    NEST,
                    6,
                    6,
                    "pet_breeding_nest",
                    createLogic: (factory, ctx) => new FurniturePetBreedingNestLogic(factory, ctx)
                )
                .Logic;

        foreach (var (petId, x) in new[] { (PET1, 4), (PET2, 5) })
        {
            (await Pets.PlacePetAsync(Owner, petId, x, 4, Ct)).Should().BeTrue();
            Pets.TryGetPet(petId, out var pet).Should().BeTrue();
            nest.AddOccupant(pet!).Should().BeTrue();
        }

        ((System.Collections.IDictionary)Member("PendingNestBreedings"))[(RoomObjectId)NEST] =
            new NestBreedingSession
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
