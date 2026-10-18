using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Texts;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The saddle a horse wears, as Habbo's furni data describes it: <c>customparams</c>
/// "&lt;petType&gt; &lt;layer&gt; &lt;part&gt; &lt;palette&gt; &lt;riderEffect&gt;" (<c>horse_saddle1</c>
/// "15 4 9 0 77", <c>horse_saddle2</c> "15 4 10 0 103"). A saddled horse carries the saddle as a
/// custom part (official client capture, room 69322463: "15 3 ffffff 3 2 -1 1 3 -1 1 4 10 0"), its
/// rider wears the saddle's effect, and taking it off gives that saddle back. Before, the horse
/// was drawn bare, every rider wore 77 and the configured saddle came back whichever was used.
/// </summary>
public sealed class PetSaddleTests
{
    private const int OWNER = 1;
    private const int PET = 40;
    private const int SADDLE = 41;
    private const int ROLLER = 42;
    private const int SADDLE1_DEFINITION = 4221;
    private const int SADDLE2_DEFINITION = 4450;
    private const string SADDLE1 = "15 4 9 0 77";
    private const string SADDLE2 = "15 4 10 0 103";

    private readonly WiredRoom _room = new(10, 10);
    private readonly RoomPlayerAvatar _owner = new() { ObjectId = 5, PlayerId = OWNER };
    private PetSnapshot _horse = Horse();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PetSaddleTests()
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
            Task.FromResult<PetSnapshot?>(_horse);
        harness.Fakes.Handlers["CanRoll"] = _ => true;
        harness.Fakes.Handlers["TryGetDefinition"] = call =>
            call.Args[0] switch
            {
                SADDLE1_DEFINITION => Definition(SADDLE1_DEFINITION, "horse_saddle1", SADDLE1),
                SADDLE2_DEFINITION => Definition(SADDLE2_DEFINITION, "horse_saddle2", SADDLE2),
                _ => null,
            };
        harness.Fakes.Handlers["TryGetDefinitionByName"] = call =>
            call.Args[0] is "horse_saddle1"
                ? Definition(SADDLE1_DEFINITION, "horse_saddle1", SADDLE1)
                : null;
    }

    private object Member(string name) => RoomHarness.GetMember(_room.Harness.State, name)!;

    private RoomPetModule Pets => _room.Harness.Module<RoomPetModule>();

    private static ActionContext Owner => ActionContext.CreateForPlayer(OWNER, 1);

    [Theory]
    [InlineData(SADDLE1_DEFINITION, SADDLE1, "15 3 FFFFFF 3 2 -1 1 3 -1 1 4 9 0", 77)]
    [InlineData(SADDLE2_DEFINITION, SADDLE2, "15 3 FFFFFF 3 2 -1 1 3 -1 1 4 10 0", 103)]
    public async Task A_saddle_is_drawn_on_the_horse_and_its_rider_wears_the_saddles_effect(
        int definitionId,
        string customParams,
        string figure,
        int effectId
    )
    {
        await PlaceAsync();

        (await SaddleAsync(definitionId, customParams)).Should().BeTrue();

        var pet = Pet();
        pet.HasSaddle.Should().BeTrue();
        ClientFigure().Should().Be(figure, "the saddle is a custom part, as Habbo sends it");

        (await Pets.MountPetAsync(Owner, PET, true, Ct)).Should().BeTrue();

        _owner.GetSnapshot().EffectId.Should().Be(effectId);
    }

    [Fact]
    public async Task Taking_the_saddle_off_gives_back_the_one_that_was_on_and_draws_the_horse_bare()
    {
        await PlaceAsync();
        (await SaddleAsync(SADDLE2_DEFINITION, SADDLE2)).Should().BeTrue();

        (await Pets.RemovePetSaddleAsync(Owner, PET, Ct)).Should().BeTrue();

        Granted().Should().Equal(SADDLE2_DEFINITION);
        Pet().HasSaddle.Should().BeFalse();
        ClientFigure().Should().Be("15 3 FFFFFF 2 2 -1 1 3 -1 1");
    }

    // The used saddle's row and the saddled horse were both left for the next flush, so a crash
    // could undo the use; they are written together, at once.
    [Fact]
    public async Task Strapping_a_saddle_on_writes_its_deletion_together_with_the_saddled_horse()
    {
        await PlaceAsync();

        (await SaddleAsync(SADDLE2_DEFINITION, SADDLE2)).Should().BeTrue();

        var write = _room.Harness.Fakes.Log.Of("WriteUseAsync").Should().ContainSingle().Subject;
        ((UsedUpItem)write.Args[1]!).Item.ObjectId.Should().Be((RoomObjectId)SADDLE);
        ((List<PetSnapshot>)write.Args[2]!)
            .Should()
            .ContainSingle(x =>
                x.Id == PET && x.HasSaddle && x.SaddleDefinitionId == SADDLE2_DEFINITION
            );
        _room.Harness.Fakes.Log.Of("EnqueueDeletedItemAsync").Should().BeEmpty();
    }

    // The saddle given back is written at once by the inventory; the bare horse waited for the
    // next hand-over, so a crash in between left the horse saddled with its saddle back in the
    // inventory, to be taken off again.
    [Fact]
    public async Task Taking_the_saddle_off_writes_the_bare_horse_before_the_saddle_is_given_back()
    {
        await PlaceAsync();
        (await SaddleAsync(SADDLE2_DEFINITION, SADDLE2)).Should().BeTrue();

        (await Pets.RemovePetSaddleAsync(Owner, PET, Ct)).Should().BeTrue();

        var calls = _room.Harness.Fakes.Log.Calls.ToList();
        var bare = calls.FindLastIndex(x =>
            x.Method == "WriteUseAsync"
            && x.Args[2] is List<PetSnapshot> pets
            && pets.Any(p => p.Id == PET && !p.HasSaddle)
        );
        bare.Should().BeGreaterThanOrEqualTo(0, "the bare horse is written at once");
        bare.Should()
            .BeLessThan(
                calls.FindIndex(x => x.Method == nameof(IInventoryGrain.GrantFurnitureAsync))
            );
    }

    [Fact]
    public async Task A_horse_keeps_which_saddle_it_wears_when_picked_up_and_placed_again()
    {
        await PlaceAsync();
        (await SaddleAsync(SADDLE2_DEFINITION, SADDLE2)).Should().BeTrue();
        PetSnapshot? returned = null;
        _room.Harness.Fakes.Handlers[nameof(IInventoryGrain.ReturnPetAsync)] = call =>
        {
            returned = (PetSnapshot)call.Args[0]!;

            return Task.FromResult(true);
        };

        // What the room hands back to the inventory is what it is placed with next time.
        (await Pets.PickupPetAsync(Owner, PET, Ct))
            .Should()
            .BeTrue();
        returned!.SaddleDefinitionId.Should().Be(SADDLE2_DEFINITION);
        _horse = returned;
        await PlaceAsync();
        ClientFigure().Should().EndWith(" 4 10 0");
        (await Pets.MountPetAsync(Owner, PET, true, Ct)).Should().BeTrue();

        _owner.GetSnapshot().EffectId.Should().Be(103);
    }

    [Fact]
    public async Task A_rider_rolled_on_a_roller_stays_on_the_horse()
    {
        var roller = _room.AddFloorItem(
            ROLLER,
            4,
            4,
            "roller",
            createLogic: (factory, ctx) => new FurnitureRollerLogic(factory, ctx)
        );
        roller.SetRotation(Rotation.East);
        RoomHarness.SetMember(_room.Harness.State, "IsFurniLoaded", true);
        await _room
            .Harness.Module<RoomRollerSystem>()
            .OnRoomEventAsync(
                new RoomRollerChangedEvent
                {
                    RoomId = 1,
                    CausedBy = Owner,
                    ObjectId = ROLLER,
                },
                Ct
            );
        _horse = _horse with { HasSaddle = true };
        await PlaceAsync();
        (await Pets.MountPetAsync(Owner, PET, true, Ct)).Should().BeTrue();
        var lift = _owner.Z.Value - Pet().Z.Value;

        await _room.Harness.Module<RoomRollerSystem>().ProcessRollersAsync(10_000, Ct);
        await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(10_000, Ct);

        (_owner.X, _owner.Y).Should().Be((5, 4));
        (Pet().X, Pet().Y).Should().Be((5, 4));
        (_owner.Z.Value - Pet().Z.Value).Should().Be(lift).And.BeGreaterThan(0);
    }

    /// <summary>
    /// Official saddled horses never sit or lie down (official client captures 2026-10-17/18: 13
    /// saddled horses, ~330 updates, 0 sit / 0 lay) and the horse has no saddle drawn lying. Told
    /// to, ours did.
    /// </summary>
    [Theory]
    [InlineData("Rex lie down")]
    [InlineData("Rex down")]
    [InlineData("Rex sit")]
    public async Task A_saddled_horse_told_to_sit_or_lie_down_stays_on_its_feet(string command)
    {
        _horse = _horse with { HasSaddle = true };
        await PlaceAsync();

        await SayAsync(command);

        Status().Should().NotContain("/lay").And.NotContain("/sit");
    }

    [Fact]
    public async Task A_horse_lying_down_gets_up_when_it_is_saddled()
    {
        await PlaceAsync();
        await SayAsync("Rex lie down");
        Status().Should().Contain("/lay ", "an unsaddled horse lies down when told");

        (await SaddleAsync(SADDLE1_DEFINITION, SADDLE1)).Should().BeTrue();

        Status().Should().NotContain("/lay");
    }

    /// <summary>
    /// Left to itself an official horse blinks, wags, paws with a front leg ("kck") and shows
    /// the heart (gst sml), each for about a second; it lies down only unsaddled and never sits
    /// (no sit posture). The need icons (thr / trd / plf / hng) are no idle filler. Ours sat, lay
    /// and wagged, whatever it wore.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_free_horse_on_its_own_paws_blinks_and_smiles_and_rests_only_unsaddled(
        bool saddled
    )
    {
        _horse = _horse with { HasSaddle = saddled };
        await PlaceAsync();
        await SayAsync("Rex free");
        var seen = new List<string>();

        // The actions are weighted by how often official horses showed them; stepping through
        // the whole weight range reaches each.
        for (var pick = 0; pick < 235; pick += 5)
        {
            RoomHarness.SetField(Pets, "_random", new FixedRandom(pick));
            await _room.Harness.Room.PetTickSystem.ProcessPetsAsync((pick + 1) * 1_000_000L, Ct);
            seen.Add(Status());
        }

        seen.Should().Contain(x => x.Contains("/eyb"));
        seen.Should().Contain(x => x.Contains("/kck"));
        seen.Should().Contain(x => x.Contains("/gst sml/"));
        seen.Should().NotContain(x => x.Contains("/sit"));
        seen.Should()
            .NotContain(x =>
                x.Contains("/gst thr")
                || x.Contains("/gst trd")
                || x.Contains("/gst plf")
                || x.Contains("/gst hng")
            );

        if (saddled)
            seen.Should().NotContain(x => x.Contains("/lay"));
        else
            seen.Should().Contain(x => x.Contains("/lay "));
    }

    /// <summary>
    /// Habbo's texts carry what a horse says while it drinks (pet.vocals.15.DRINKING.0 "Ah, that's
    /// the good stuff.", user 2026-10-18 in official room 30598515); the client never reads them, so
    /// the room says them. Ours drank in silence.
    /// </summary>
    [Fact]
    public async Task A_horse_drinking_says_the_hotels_drinking_line()
    {
        const string LINE = "Ah, that's the good stuff.";
        _room.Harness.Fakes.Handlers["GetTextsByPrefixAsync"] = call =>
            Task.FromResult(
                (string)call.Args[0]! == "pet.vocals.15.DRINKING."
                    ? new HotelTexts(
                        new Dictionary<string, string> { ["pet.vocals.15.DRINKING.0"] = LINE }
                    )
                    : HotelTexts.Empty
            );
        await PlaceAsync();
        _room.AddFloorItem(
            ROLLER,
            4,
            4,
            "pet_drink",
            createLogic: (factory, ctx) => new FurniturePetDrinkLogic(factory, ctx)
        );

        _room.Harness.Fakes.Handlers["ApplyPetNutritionOperationAsync"] = _ =>
            Task.FromResult(new PetNutritionOperationResult { Nutrition = 1, ActualGain = 1 });

        await SayAsync("Rex drink");
        await _room.Harness.Room.PetTickSystem.ProcessPetsAsync(1_000_000L, Ct);

        // Official horses stand at the drink a moment before they drink and say it.
        await _room.Harness.Room.TimerSystem.ProcessTimersAsync(long.MaxValue, Ct);

        _room
            .Harness.Fakes.Log.Calls.SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<ChatMessageComposer>()
            .Where(x => x.ObjectId != _owner.ObjectId)
            .Select(x => x.Text)
            .Should()
            .Equal(LINE);
    }

    private string Status() => ((RoomPetAvatarSnapshot)Pet().GetSnapshot()).Status;

    private Task SayAsync(string text) =>
        _room.Harness.Room.ChatSystem.SendChatFromPlayerAsync(
            Owner,
            RoomChatType.Chat,
            text,
            0,
            0,
            null,
            Ct
        );

    /// <summary>Never walks (a percent chance draws 99) and otherwise picks the given value, kept in range.</summary>
    private sealed class FixedRandom(int pick) : Random
    {
        public override int Next(int maxValue) =>
            maxValue == 100 ? 99 : Math.Min(pick, maxValue - 1);

        public override int Next(int minValue, int maxValue) =>
            Math.Clamp(minValue + pick, minValue, Math.Max(minValue, maxValue - 1));
    }

    private async Task PlaceAsync() =>
        (await Pets.PlacePetAsync(Owner, PET, 4, 4, Ct)).Should().BeTrue();

    private Task<bool> SaddleAsync(int definitionId, string customParams)
    {
        _room.AddFloorItem(
            SADDLE,
            2,
            2,
            "pet_saddle",
            definitionId: definitionId,
            createLogic: (factory, ctx) => new FurniturePetSaddleLogic(factory, ctx),
            customParams: customParams
        );

        return _room.Harness.Room.InteractWithItemAsync(
            Owner,
            SADDLE,
            new UseWithPetInteraction { PetId = PET },
            Ct
        );
    }

    private IRoomPet Pet()
    {
        Pets.TryGetPet(PET, out var pet).Should().BeTrue();

        return pet!;
    }

    /// <summary>The figure the room sends the clients for the horse.</summary>
    private string ClientFigure() => ((RoomPetAvatarSnapshot)Pet().GetSnapshot()).Figure;

    private IEnumerable<int> Granted() =>
        _room
            .Harness.Fakes.Log.Calls.Where(x =>
                x.Method == nameof(IInventoryGrain.GrantFurnitureAsync)
            )
            .Select(x => (int)x.Args[0]!);

    private static FurnitureDefinitionSnapshot Definition(
        int id,
        string name,
        string customParams
    ) =>
        new()
        {
            Id = id,
            SpriteId = id,
            Name = name,
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.PetSaddle,
            LogicName = "pet_saddle",
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
