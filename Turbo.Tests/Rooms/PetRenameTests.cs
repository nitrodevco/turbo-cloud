using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The info stand's rename dialog (client build 116): a pet's owner gives it a new name where it
/// stands, the room sees it under that name, and its row holds the name before the dialog is
/// answered, because the client then asks for its pets tab again. The client hides the button on
/// a dead monsterplant waiting to be revived, and nobody renames someone else's pet.
/// </summary>
public sealed class PetRenameTests
{
    private const int OWNER = 9;
    private const int VISITOR = 10;
    private const int PET = 40;
    private const int CAT = 1;

    private readonly LiveRoomHarness _harness = new(10, 10);
    private readonly RoomPlayerAvatar _owner = new() { ObjectId = 5, PlayerId = OWNER };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PetRenameTests()
    {
        _owner.SetPosition(2, 2);
        _harness.State.AvatarsByObjectId[5] = _owner;
        _harness.State.AvatarsByPlayerId[OWNER] = 5;
        _harness.Module<RoomMapModule>().AddAvatar(_owner, false);
        _harness.State.RoomSnapshot = _harness.State.RoomSnapshot with
        {
            AllowPets = true,
            Name = "Garden",
            Description = "Where Rex lives",
        };
        _harness.Room.UseEventSystem(new TestEventBus().System);
        _harness.Room.UseAvatarProvider(new RoomAvatarProvider(Options.Create(new BotConfig())));
        // The room's own pets, read once when a rename finds them not loaded yet: none.
        _harness.Fakes.Handlers["LoadPetsByRoomIdAsync"] = _ =>
            Task.FromResult<IReadOnlyList<PetSnapshot>>([]);
    }

    private static ActionContext Owner => ActionContext.CreateForPlayer(OWNER, 1);

    [Fact]
    public async Task The_owner_renames_their_pet_and_the_room_sees_it_under_the_new_name()
    {
        await PlaceAsync(Pet(CAT));

        (await _harness.Room.RenamePetAsync(Owner, PET, "  Fido ", Ct)).Should().BeTrue();

        var sent = _harness
            .Fakes.SentToRoom<IComposer>()
            .Where(x => x is UserRemoveMessageComposer or UsersMessageComposer)
            .ToList();
        var objectId = Arrival(sent[^1]).ObjectId;

        sent.TakeLast(2)
            .Select(x => x.GetType())
            .Should()
            .Equal(typeof(UserRemoveMessageComposer), typeof(UsersMessageComposer));
        ((UserRemoveMessageComposer)sent[^2]).ObjectId.Should().Be(objectId);
        Arrival(sent[^1]).Name.Should().Be("Fido");
    }

    [Fact]
    public async Task The_name_is_written_before_the_rename_is_answered()
    {
        await PlaceAsync(Pet(CAT));

        (await _harness.Room.RenamePetAsync(Owner, PET, "Fido", Ct)).Should().BeTrue();

        _harness
            .Fakes.Log.Of("WriteUseAsync")
            .Select(x => x.Args.OfType<List<PetSnapshot>>().Single())
            .Should()
            .Contain(x => x.Count == 1 && x[0].Id == PET && x[0].Name == "Fido");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Sixteen letters!")]
    [InlineData("Rex<3")]
    public async Task A_name_a_pet_may_not_have_is_refused(string name)
    {
        await PlaceAsync(Pet(CAT));

        (await _harness.Room.RenamePetAsync(Owner, PET, name, Ct)).Should().BeFalse();

        NameInRoom().Should().Be("Rex");
    }

    [Fact]
    public async Task A_name_holding_a_filtered_word_is_refused()
    {
        await PlaceAsync(Pet(CAT));
        _harness.Room.UseWordFilter(new NothingIsCleanFilter());

        (await _harness.Room.RenamePetAsync(Owner, PET, "Fido", Ct)).Should().BeFalse();

        NameInRoom().Should().Be("Rex");
    }

    [Fact]
    public async Task Nobody_but_its_owner_renames_a_pet_not_even_the_rooms_owner()
    {
        await PlaceAsync(Pet(CAT));
        _harness.State.RoomSnapshot = _harness.State.RoomSnapshot with { OwnerId = VISITOR };

        (
            await _harness.Room.RenamePetAsync(
                ActionContext.CreateForPlayer(VISITOR, 1),
                PET,
                "Mine",
                Ct
            )
        )
            .Should()
            .BeFalse();
        (await _harness.Room.RenamePetAsync(Owner, 999, "Fido", Ct))
            .Should()
            .BeFalse("no such pet stands here");

        NameInRoom().Should().Be("Rex");
    }

    [Fact]
    public async Task A_dead_monsterplant_waiting_to_be_revived_is_not_renamed_and_a_living_one_is()
    {
        var watered = _harness.Clock.GetUtcNow().UtcDateTime;

        await PlaceAsync(Pet(PetTypes.MONSTERPLANT) with { Level = 7, WateredAtUtc = watered });

        (await _harness.Room.RenamePetAsync(Owner, PET, "Audrey", Ct)).Should().BeTrue();

        // Its wellbeing runs out: the plant is dead until a revival potion.
        _harness.Clock.Advance(
            TimeSpan.FromSeconds(new PetConfig().MonsterplantWellBeingSeconds + 1)
        );

        (await _harness.Room.RenamePetAsync(Owner, PET, "Seymour", Ct)).Should().BeFalse();

        NameInRoom().Should().Be("Audrey");
    }

    [Fact]
    public async Task A_respect_names_the_room_for_a_client_that_reads_where_a_pet_is()
    {
        await PlaceAsync(Pet(CAT));

        _harness.Fakes.Handlers["ApplyPetRespectOperationAsync"] = _ =>
            Task.FromResult(new PetRespectOperationResult { Accepted = true, Respect = 1 });

        (await _harness.Module<RoomPetModule>().RespectPetAsync(Owner, PET, Ct)).Should().BeTrue();

        _harness
            .Fakes.SentToRoom<PetRespectNotificationEventMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Room.Should()
            .Be(
                new PetRoomSnapshot
                {
                    RoomId = _harness.Room.RoomId,
                    Name = "Garden",
                    Description = "Where Rex lives",
                }
            );
    }

    private static RoomPetAvatarSnapshot Arrival(IComposer users) =>
        ((UsersMessageComposer)users).Avatars.OfType<RoomPetAvatarSnapshot>().Single();

    private string NameInRoom()
    {
        _harness.Module<RoomPetModule>().TryGetPet(PET, out var pet).Should().BeTrue();

        return pet!.Name;
    }

    private async Task PlaceAsync(PetSnapshot pet)
    {
        _harness.Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutPetAsync)] = _ =>
            Task.FromResult<PetSnapshot?>(pet);

        (await _harness.Module<RoomPetModule>().PlacePetAsync(Owner, PET, 4, 4, Ct))
            .Should()
            .BeTrue();
    }

    private PetSnapshot Pet(int typeId) =>
        new()
        {
            Id = PET,
            OwnerId = OWNER,
            OwnerName = "owner",
            RoomId = null,
            Name = "Rex",
            Figure = new PetFigureSnapshot
            {
                TypeId = typeId,
                PaletteId = 0,
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
            CreatedAtUtc = _harness.Clock.GetUtcNow().UtcDateTime,
            WateredAtUtc = _harness.Clock.GetUtcNow().UtcDateTime,
            HarvestedAtUtc = null,
        };

    /// <summary>A hotel filter that finds a forbidden word in everything.</summary>
    private sealed class NothingIsCleanFilter : IWordFilter
    {
        public string Replacement => "bobba";

        public string Filter(string text) => Replacement;

        public string Filter(string text, IReadOnlySet<string> extraWords) => Replacement;

        public bool IsClean(string text) => false;

        public Task ReloadAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
