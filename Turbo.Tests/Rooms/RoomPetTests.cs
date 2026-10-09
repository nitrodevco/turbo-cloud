using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A pet in a live room, as its owner sees it: scratched, it shows the scratch to the room; told
/// to sit it sits on the floor, not above it; it turns its head like an avatar, never over its
/// shoulder; and it says what its own kind says.
/// </summary>
public sealed class RoomPetTests
{
    private const int OWNER = 9;
    private const int PET = 40;
    private const int CAT = 1;

    private readonly LiveRoomHarness _harness = new(10, 10);
    private readonly RoomPlayerAvatar _owner = new() { ObjectId = 5, PlayerId = OWNER };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomPetTests()
    {
        _owner.SetPosition(2, 2);
        ((IDictionary<RoomObjectId, IRoomAvatar>)Member("AvatarsByObjectId"))[5] = _owner;
        ((IDictionary<PlayerId, RoomObjectId>)Member("AvatarsByPlayerId"))[OWNER] = 5;
        _harness.Module<RoomMapModule>().AddAvatar(_owner, false);
        RoomHarness.SetMember(Member("RoomSnapshot"), "AllowPets", true);
        RoomHarness.SetField(_harness.Room, "_eventSystem", new TestEventBus().System);
        RoomHarness.SetField(
            _harness.Room,
            "_avatarProvider",
            new RoomAvatarProvider(Options.Create(new BotConfig()))
        );

        _harness.Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutPetAsync)] = _ =>
            Task.FromResult<PetSnapshot?>(Pet());
    }

    private object Member(string name) => RoomHarness.GetMember(_harness.State, name)!;

    private RoomPetModule Pets => _harness.Module<RoomPetModule>();

    private static ActionContext Owner => ActionContext.CreateForPlayer(OWNER, 1);

    [Fact]
    public async Task AScratch_IsShownToTheRoom()
    {
        await PlaceAsync();

        _harness.Fakes.Handlers["ApplyPetRespectOperationAsync"] = _ =>
            Task.FromResult(new PetRespectOperationResult { Accepted = true, Respect = 1 });

        (await Pets.RespectPetAsync(Owner, PET, Ct)).Should().BeTrue();

        SentToRoom<PetRespectNotificationEventMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Pet.Id.Should()
            .Be(PET);
    }

    [Theory]
    [InlineData("sit", "sit")]
    [InlineData("lay", "lay")]
    public async Task APetToldToSitOrLie_DoesSoOnTheFloor_NotAboveIt(string word, string status)
    {
        await PlaceAsync();

        await SayAsync($"Rex {word}");

        PetSnapshot().Status.Should().Contain($"/{status} 0/");
    }

    [Fact]
    public async Task APetHearingSomeoneBehindIt_DoesNotTurnItsHeadOverItsShoulder()
    {
        await PlaceAsync(); // standing at 4,4 facing south

        _owner.SetPosition(2, 2); // north-west of it, behind

        await SayAsync("hello");

        PetSnapshot().HeadRotation.Should().Be(Rotation.South);
    }

    [Fact]
    public async Task APetHearingSomeoneToOneSide_LooksAtThem()
    {
        await PlaceAsync();

        MoveOwnerTo(3, 5); // south-west of it, one octant off its body

        await SayAsync("hello");

        PetSnapshot().HeadRotation.Should().Be(Rotation.SouthWest);
    }

    [Fact]
    public async Task APetLookingRound_TurnsItsHeadWithItsBody()
    {
        await PlaceAsync();
        MoveOwnerTo(6, 4); // east: nowhere near the direction the pet will look round to
        await SayAsync("Rex free");
        RoomHarness.SetField(Pets, "_random", new HighestRandom());

        await _harness.Room.PetTickSystem.ProcessPetsAsync(long.MaxValue / 2, Ct);

        var pet = PetSnapshot();
        pet.BodyRotation.Should().Be(Rotation.NorthWest, "the highest of eight directions");
        pet.HeadRotation.Should().Be(pet.BodyRotation);
    }

    [Fact]
    public async Task APetFollowingItsOwner_TurnsItsHeadWithItsBody()
    {
        await PlaceAsync();
        MoveOwnerTo(5, 4); // beside it, so it follows by facing the way its owner faces
        _owner.SetRotation(Rotation.NorthEast);
        await SayAsync("Rex follow");

        await _harness.Room.PetTickSystem.ProcessPetsAsync(0, Ct);

        var pet = PetSnapshot();
        pet.BodyRotation.Should().Be(Rotation.NorthEast);
        pet.HeadRotation.Should().Be(Rotation.NorthEast);
    }

    [Fact]
    public async Task ACatToldToSpeak_SaysWhatACatSays()
    {
        _harness.Fakes.Handlers[nameof(IPetSpeechProvider.GetLines)] = call =>
            (int)call.Args[0]! == CAT
                ? ImmutableArray.Create("Meow!")
                : ImmutableArray.Create("Woof!");

        await PlaceAsync();
        await SayAsync("Rex speak");

        SentToRoom<ChatMessageComposer>()
            .Where(x => x.ObjectId != _owner.ObjectId)
            .Select(x => x.Text)
            .Should()
            .Equal("Meow!");
    }

    private async Task PlaceAsync() =>
        (await Pets.PlacePetAsync(Owner, PET, 4, 4, Ct)).Should().BeTrue();

    private void MoveOwnerTo(int x, int y) => _owner.SetPosition(x, y);

    private Task SayAsync(string text) =>
        _harness.Room.ChatSystem.SendChatFromPlayerAsync(
            Owner,
            RoomChatType.Chat,
            text,
            0,
            0,
            null,
            Ct
        );

    private RoomPetAvatarSnapshot PetSnapshot()
    {
        Pets.TryGetPet(PET, out var pet).Should().BeTrue();

        return (RoomPetAvatarSnapshot)pet!.GetSnapshot();
    }

    private IEnumerable<T> SentToRoom<T>() =>
        _harness
            .Fakes.Log.Calls.SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<T>();

    private static PetSnapshot Pet() =>
        new()
        {
            Id = PET,
            OwnerId = OWNER,
            OwnerName = "owner",
            RoomId = null,
            Name = "Rex",
            Figure = new PetFigureSnapshot
            {
                TypeId = CAT,
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
            CreatedAtUtc = DateTime.UtcNow,
            WateredAtUtc = DateTime.UtcNow,
            HarvestedAtUtc = null,
        };

    /// <summary>Always the top of the range: an idle pet that looks round, to the last direction.</summary>
    private sealed class HighestRandom : Random
    {
        public override int Next(int maxValue) => Math.Max(0, maxValue - 1);

        public override int Next(int minValue, int maxValue) => Math.Max(minValue, maxValue - 1);
    }
}
