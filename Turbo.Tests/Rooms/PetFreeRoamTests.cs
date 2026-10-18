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
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// What a free pet does on its own around furni, from official client captures: pets walk onto
/// sofas and sit there at 0.45 (2026-10-18, 10 of 10 seat visits, types 12/17/37; players on the
/// same sofas sit at 1.0), and horses go to a rubber ball, kick it with a smile while the ball
/// shows state "1" for 3.5 s (2026-10-17, room "saddle club", 6 of 6 plays). Ours kept every pet
/// off seats and never went to a toy unasked.
/// </summary>
public sealed class PetFreeRoamTests
{
    private const int OWNER = 1;
    private const int PET = 40;
    private const int SEAT = 41;
    private const int TOY = 42;

    private readonly WiredRoom _room = new(10, 10);
    private readonly RoomPlayerAvatar _owner = new() { ObjectId = 5, PlayerId = OWNER };
    private PetSnapshot _pet = Horse();
    private long _now;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PetFreeRoamTests()
    {
        var harness = _room.Harness;

        _owner.SetPosition(1, 1);
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
            Task.FromResult<PetSnapshot?>(_pet);
    }

    [Fact]
    public async Task A_free_pet_wanders_onto_a_free_seat_and_sits_there_at_the_pets_seat_height()
    {
        _room.AddFloorItem(SEAT, 5, 5, canWalk: false, canSit: true);
        await PlaceFreeAsync();

        await DecideAsync(new ScriptedRandom(step: 1));
        await WalkAsync();

        var pet = Pet();
        (pet.X, pet.Y).Should().Be((5, 5), "official free pets walk onto sofas");
        Status().Should().Contain("/sit 0.45", "a pet sits at 0.45 where a player sits at 1.0");
        pet.Z.Should().Be(Altitude.Zero, "the pet stays at the seat's own height");
    }

    [Fact]
    public async Task A_seated_pet_keeps_its_seat_through_its_idle_postures()
    {
        _room.AddFloorItem(SEAT, 5, 5, canWalk: false, canSit: true);
        await PlaceFreeAsync();
        await DecideAsync(new ScriptedRandom(step: 1));
        await WalkAsync();

        // Weight 111 is past "std" (0-87), within "eyb" (88-138): the horse blinks.
        await DecideAsync(new ScriptedRandom(step: 111, chance: 99));

        Status().Should().Contain("/sit 0.45").And.Contain("/eyb");
    }

    [Fact]
    public async Task A_saddled_horse_keeps_off_seats()
    {
        _pet = _pet with { HasSaddle = true };
        _room.AddFloorItem(SEAT, 5, 5, canWalk: false, canSit: true);
        await PlaceFreeAsync();

        await DecideAsync(new ScriptedRandom(step: 1));
        await WalkAsync();

        Status().Should().NotContain("/sit");
        (Pet().X, Pet().Y).Should().NotBe((5, 5));
    }

    [Fact]
    public async Task A_free_horse_goes_to_a_toy_on_its_own_and_kicks_it_while_the_toy_shows_it()
    {
        var toy = _room.AddFloorItem(
            TOY,
            5,
            5,
            "pet_toy",
            createLogic: (factory, ctx) => new FurniturePetToyLogic(factory, ctx)
        );
        await PlaceFreeAsync();

        await DecideAsync(new ScriptedRandom(step: 1));
        await WalkAsync();
        await DecideAsync(new ScriptedRandom(step: 1, chance: 99));

        Status().Should().Contain("/kck").And.Contain("/gst sml");
        toy.Logic.StuffData.GetLegacyString().Should().Be("1");

        // The room's timers run on the room clock: run every one due by then.
        await _room.Harness.Room.TimerSystem.ProcessTimersAsync(long.MaxValue, Ct);

        toy.Logic.StuffData.GetLegacyString()
            .Should()
            .NotBe("1", "the ball rests again after the play");
    }

    private object Member(string name) => RoomHarness.GetMember(_room.Harness.State, name)!;

    private RoomPetModule Pets => _room.Harness.Module<RoomPetModule>();

    private IRoomPet Pet()
    {
        Pets.TryGetPet(PET, out var pet).Should().BeTrue();

        return pet!;
    }

    private string Status() => ((RoomPetAvatarSnapshot)Pet().GetSnapshot()).Status;

    private async Task PlaceFreeAsync()
    {
        (await Pets.PlacePetAsync(ActionContext.CreateForPlayer(OWNER, 1), PET, 4, 4, Ct))
            .Should()
            .BeTrue();
        await _room.Harness.Room.ChatSystem.SendChatFromPlayerAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            RoomChatType.Chat,
            "Rex free",
            0,
            0,
            null,
            Ct
        );
    }

    /// <summary>One decision of the pet's, with the given dice, well after its last.</summary>
    private Task DecideAsync(Random random)
    {
        RoomHarness.SetField(Pets, "_random", random);
        _now += 1_000_000L;

        return _room.Harness.Room.PetTickSystem.ProcessPetsAsync(_now, Ct);
    }

    private async Task WalkAsync()
    {
        for (var step = 0; step < 10 && Pet().IsWalking; step++)
        {
            _now += 1_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
        }

        _now += 1_000;
        await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
    }

    /// <summary>
    /// Every range roll lands <paramref name="step"/> above its minimum (a wander goes one tile
    /// right and down); a percent roll comes up <paramref name="chance"/> (0: every chance taken).
    /// </summary>
    private sealed class ScriptedRandom(int step, int chance = 0) : Random
    {
        public override int Next(int maxValue) =>
            maxValue == 100 ? chance : Math.Min(0, maxValue - 1);

        public override int Next(int minValue, int maxValue) =>
            minValue < 0
                ? Math.Clamp(step, minValue, maxValue - 1)
                : Math.Clamp(minValue + step, minValue, Math.Max(minValue, maxValue - 1));
    }

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
