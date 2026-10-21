using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Action;
using Turbo.Primitives.Bots.Enums;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Outgoing.Room.Bots;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
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
    private const int PET2 = 41;
    private const int BOT = 50;
    private const int BOT2 = 51;
    private const int CAT = 1;

    private readonly LiveRoomHarness _harness = new(10, 10);
    private readonly RoomPlayerAvatar _owner = new() { ObjectId = 5, PlayerId = OWNER };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomPetTests()
    {
        _owner.SetPosition(2, 2);
        _harness.State.AvatarsByObjectId[5] = _owner;
        _harness.State.AvatarsByPlayerId[OWNER] = 5;
        _harness.Module<RoomMapModule>().AddAvatar(_owner, false);
        _harness.State.RoomSnapshot = _harness.State.RoomSnapshot with { AllowPets = true };
        _harness.Room.UseEventSystem(new TestEventBus().System);
        _harness.Room.UseAvatarProvider(new RoomAvatarProvider(Options.Create(new BotConfig())));

        _harness.Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutPetAsync)] = _ =>
            Task.FromResult<PetSnapshot?>(Pet());
    }

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

        PetSnapshot().Status.Should().Contain($"/{status} 0.0/");
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
        Pets.UseRandom(new HighestRandom());

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

    [Theory]
    [InlineData(OWNER, false)]
    [InlineData(77, true)]
    public async Task Putting_ones_pet_down_in_someone_elses_room_counts_a_pet_visit(
        int roomOwnerId,
        bool counts
    )
    {
        // Habboween 2026 "Take your pet to visit a friend" (pet_visit, PET_OTHER_USERS_ROOM).
        _harness.State.RoomSnapshot = _harness.State.RoomSnapshot with
        {
            OwnerId = (PlayerId)roomOwnerId,
        };

        await PlaceAsync();

        _harness
            .Fakes.Log.Of("RecordActionAsync")
            .Select(x => x.Args[0])
            .Should()
            .Equal(counts ? [RewardTrackActionTypes.PET_OTHER_USERS_ROOM] : []);
    }

    /// <summary>
    /// Two pets put down on one tile while the first is still being taken out of the inventory:
    /// both pass the tile check and the first stands there. A pet put on a taken tile is refused
    /// (PetPlacingError 4, RSEME_SELECTED_TILE_NOT_FREE_FOR_PET: RoomUsersHandler.as:523-525) and
    /// stays its owner's; ours put the second down on another tile instead.
    /// </summary>
    [Fact]
    public async Task A_second_pet_put_on_the_same_tile_meanwhile_is_refused_and_given_back()
    {
        var checkOuts = new Dictionary<int, TaskCompletionSource<PetSnapshot?>>
        {
            [PET] = new(),
            [PET2] = new(),
        };
#pragma warning disable VSTHRD003 // Each check-out is held open until the test completes it.
        _harness.Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutPetAsync)] = call =>
            checkOuts[(int)call.Args[0]!].Task;
#pragma warning restore VSTHRD003
        _harness.Fakes.Handlers[nameof(IInventoryGrain.ReturnPetAsync)] = _ =>
            Task.FromResult(true);

        var first = _harness.Room.PlacePetAsync(Owner, PET, 4, 4, Ct);
        var second = _harness.Room.PlacePetAsync(Owner, PET2, 4, 4, Ct);

        checkOuts[PET].SetResult(Pet());
        (await first).Should().BeTrue();
        checkOuts[PET2].SetResult(Pet() with { Id = PET2 });
        (await second).Should().BeFalse("the tile was taken while the inventory was asked");

        Pets.TryGetPet(PET2, out _).Should().BeFalse();
        _harness
            .Fakes.Log.Of(nameof(IInventoryGrain.ReturnPetAsync))
            .Select(x => ((PetSnapshot)x.Args[0]!).Id)
            .Should()
            .Equal(PET2);
        Sent<PetPlacingErrorMessageComposer>()
            .Select(x => x.Error)
            .Should()
            .Equal(PetPlacingErrorType.SelectedTileNotFree);
    }

    /// <summary>
    /// A bot is put down the same way, so the same: refused with its own taken-tile error
    /// (BotError 3, RSEME_SELECTED_TILE_NOT_FREE_FOR_BOT: RoomUsersHandler.as:558-559) and given
    /// back.
    /// </summary>
    [Fact]
    public async Task A_second_bot_put_on_the_same_tile_meanwhile_is_refused_and_given_back()
    {
        _harness.State.RoomSnapshot = _harness.State.RoomSnapshot with
        {
            OwnerId = (PlayerId)OWNER,
        };
        var checkOuts = new Dictionary<int, TaskCompletionSource<BotSnapshot?>>
        {
            [BOT] = new(),
            [BOT2] = new(),
        };
#pragma warning disable VSTHRD003 // Each check-out is held open until the test completes it.
        _harness.Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutBotAsync)] = call =>
            checkOuts[(int)call.Args[0]!].Task;
#pragma warning restore VSTHRD003
        _harness.Fakes.Handlers[nameof(IInventoryGrain.ReturnBotAsync)] = _ =>
            Task.FromResult(true);

        var first = _harness.Room.PlaceBotAsync(Owner, BOT, 4, 4, Ct);
        var second = _harness.Room.PlaceBotAsync(Owner, BOT2, 4, 4, Ct);

        checkOuts[BOT].SetResult(Bot(BOT));
        (await first).Should().BeTrue();
        checkOuts[BOT2].SetResult(Bot(BOT2));
        (await second).Should().BeFalse("the tile was taken while the inventory was asked");

        _harness
            .Fakes.Log.Of(nameof(IInventoryGrain.ReturnBotAsync))
            .Select(x => ((BotSnapshot)x.Args[0]!).Id)
            .Should()
            .Equal(BOT2);
        Sent<BotErrorMessageComposer>()
            .Select(x => x.Error)
            .Should()
            .Equal(BotErrorType.SelectedTileNotFree);
    }

    private async Task PlaceAsync() =>
        (await Pets.PlacePetAsync(Owner, PET, 4, 4, Ct)).Should().BeTrue();

    private IEnumerable<T> Sent<T>() =>
        _harness.Fakes.Log.Calls.SelectMany(x => x.Args).OfType<T>();

    private static BotSnapshot Bot(int id) =>
        new()
        {
            Id = id,
            OwnerId = OWNER,
            OwnerName = "owner",
            RoomId = null,
            Name = "Bot",
            Motto = string.Empty,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            X = 0,
            Y = 0,
            Z = Altitude.Zero,
            Rotation = Rotation.South,
            FreeRoam = false,
            ChatText = string.Empty,
            AutoChat = false,
            ChatDelaySeconds = 0,
            MixSentences = false,
            DanceType = AvatarDanceType.None,
        };

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
