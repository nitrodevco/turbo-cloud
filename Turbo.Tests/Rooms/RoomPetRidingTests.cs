using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A rider on a horse, as the room sees it. The client draws a rider wherever the room puts it:
/// isRiding only hides the horse's saddle (AnimatedPetVisualization.as:252-253, :540) and the
/// riding effect only swaps the legs, at standing height (WIN63-202610151745). So a rider sent at
/// the horse's own height stands beside it; the room lifts the rider above the horse, standing and
/// at every step, and puts them back on the floor when they get off.
/// </summary>
public sealed class RoomPetRidingTests
{
    private const int OWNER = 9;
    private const int PET = 40;
    private const int HORSE = 15;

    private readonly WiredRoom _room = new(10, 10);
    private readonly LiveRoomHarness _harness;
    private readonly RoomPlayerAvatar _owner = new() { ObjectId = 5, PlayerId = OWNER };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomPetRidingTests()
    {
        _harness = _room.Harness;
        _owner.SetPosition(3, 4);
        _owner.SetLogic(_harness.Fakes.Create<IRoomPlayerLogic>(5));
        _harness.State.AvatarsByObjectId[5] = _owner;
        _harness.State.AvatarsByPlayerId[OWNER] = 5;
        _harness.Module<RoomMapModule>().AddAvatar(_owner, false);
        _harness.State.RoomSnapshot = _harness.State.RoomSnapshot with { AllowPets = true };
        _harness.Room.UseEventSystem(new TestEventBus().System);
        _harness.Room.UseAvatarProvider(new RoomAvatarProvider(Options.Create(new BotConfig())));

        _harness.Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutPetAsync)] = _ =>
            Task.FromResult<PetSnapshot?>(Horse());
    }

    private RoomPetModule Pets => _harness.Module<RoomPetModule>();

    private static ActionContext Owner => ActionContext.CreateForPlayer(OWNER, 1);

    /// <summary>Pets.RiderHeight as configured by default.</summary>
    private const double RiderHeight = 1.0;

    [Fact]
    public async Task ARider_IsLiftedOntoTheHorse_WhichStaysOnTheFloor()
    {
        await MountAsync();

        var rider = RiderSnapshot();
        var horse = HorseSnapshot();

        (rider.X, rider.Y).Should().Be((horse.X, horse.Y));
        horse.Z.Value.Should().Be(0);
        rider.Z.Value.Should().Be(RiderHeight);
    }

    [Fact]
    public async Task ARiderWalking_StepsAboveTheHorse_WhichStepsOnTheFloor()
    {
        await MountAsync();

        (await _harness.Module<RoomAvatarModule>().WalkAvatarToAsync(_owner, 6, 4, Ct))
            .Should()
            .BeTrue();
        await _harness.Room.AvatarTickSystem.ProcessAvatarsAsync(10_000, Ct);

        RiderSnapshot()
            .Status.Should()
            .Contain($"mv 5,4,{Altitude.FromValue(RiderHeight).ToStatusString()}");
        HorseSnapshot().Status.Should().Contain("mv 5,4,0.0");

        await _harness.Room.AvatarTickSystem.ProcessAvatarsAsync(20_000, Ct);

        RiderSnapshot().Z.Value.Should().Be(RiderHeight, "still riding after the step");
        HorseSnapshot().Z.Value.Should().Be(0);
    }

    [Fact]
    public async Task ARiderGettingOff_IsBackOnTheFloor()
    {
        await MountAsync();

        (await Pets.MountPetAsync(Owner, PET, false, Ct)).Should().BeTrue();

        RiderSnapshot().Z.Value.Should().Be(0);
    }

    [Fact]
    public async Task AHorseSomeoneWalksOverToRide_WaitsForThem()
    {
        _owner.SetPosition(1, 4);
        (await Pets.PlacePetAsync(Owner, PET, 4, 4, Ct)).Should().BeTrue();
        Pets.TryGetPet(PET, out var pet).Should().BeTrue();
        pet!.IsFreeRoaming = true;

        (await Pets.MountPetAsync(Owner, PET, true, Ct)).Should().BeTrue();

        for (var tick = 1; tick <= 10 && !pet.IsRiding; tick++)
        {
            await _harness.Room.PetTickSystem.ProcessPetsAsync(long.MaxValue / 2, Ct);
            await _harness.Room.AvatarTickSystem.ProcessAvatarsAsync(tick * 10_000L, Ct);
            await _harness.Room.PetTickSystem.ProcessPetsAsync(long.MaxValue / 2, Ct);
        }

        (pet.X, pet.Y).Should().Be((4, 4), "it waited where it stood");
        pet.RiderObjectId.Should().Be(_owner.ObjectId);
        RiderSnapshot().Z.Value.Should().Be(RiderHeight);
    }

    [Fact]
    public async Task ARiderRidingOntoASeat_StaysInTheSaddle_AndSitsOnOneAfterGettingOff()
    {
        // Official client capture (2026-10-18): free pets walk onto sofas, so a horse may too.
        _room.AddFloorItem(50, 6, 4, canWalk: false, canSit: true);
        _room.AddFloorItem(51, 6, 7, canWalk: false, canSit: true);
        await MountAsync();

        var avatars = _harness.Module<RoomAvatarModule>();

        (await avatars.WalkAvatarToAsync(_owner, 6, 4, Ct)).Should().BeTrue();

        for (var tick = 1; tick <= 10 && (_owner.X, _owner.Y) != (6, 4); tick++)
            await _harness.Room.AvatarTickSystem.ProcessAvatarsAsync(tick * 10_000L, Ct);
        await _harness.Room.AvatarTickSystem.ProcessAvatarsAsync(200_000L, Ct);

        (_owner.X, _owner.Y).Should().Be((6, 4));
        (HorseSnapshot().X, HorseSnapshot().Y).Should().Be((6, 4));
        RiderSnapshot().Status.Should().NotContain("sit", "a rider is never seated");

        (await Pets.MountPetAsync(Owner, PET, false, Ct)).Should().BeTrue();
        (await avatars.WalkAvatarToAsync(_owner, 6, 7, Ct)).Should().BeTrue();

        for (var tick = 21; tick <= 30 && (_owner.X, _owner.Y) != (6, 7); tick++)
            await _harness.Room.AvatarTickSystem.ProcessAvatarsAsync(tick * 10_000L, Ct);
        await _harness.Room.AvatarTickSystem.ProcessAvatarsAsync(400_000L, Ct);

        RiderSnapshot().Status.Should().Contain("sit", "off the horse a chair seats as before");
    }

    [Fact]
    public async Task ARiderPutOnASeat_StaysInTheSaddle()
    {
        _room.AddFloorItem(50, 6, 4, canWalk: false, canSit: true);
        await MountAsync();

        await _harness
            .Module<RoomAvatarModule>()
            .RelocateAvatarAsync(_owner, _harness.Module<RoomMapModule>().ToIdx(6, 4), Ct);
        await _harness.Room.AvatarTickSystem.ProcessAvatarsAsync(10_000L, Ct);

        (_owner.X, _owner.Y).Should().Be((6, 4));
        RiderSnapshot().Status.Should().NotContain("sit");
        _owner.HasStatus(AvatarStatusType.Sit).Should().BeFalse();
    }

    [Fact]
    public async Task ARider_CannotSitDown()
    {
        await MountAsync();

        (
            await _harness
                .Module<RoomAvatarModule>()
                .SetAvatarPostureAsync(_owner.ObjectId, AvatarPostureType.Sit, Ct)
        )
            .Should()
            .BeFalse();
        _owner.HasStatus(AvatarStatusType.Sit).Should().BeFalse();
    }

    private async Task MountAsync()
    {
        (await Pets.PlacePetAsync(Owner, PET, 4, 4, Ct)).Should().BeTrue();
        (await Pets.MountPetAsync(Owner, PET, true, Ct)).Should().BeTrue();
        Pets.TryGetPet(PET, out var pet).Should().BeTrue();
        pet!.RiderObjectId.Should().Be(_owner.ObjectId);
    }

    private RoomAvatarSnapshot RiderSnapshot() => _owner.GetSnapshot();

    private RoomPetAvatarSnapshot HorseSnapshot()
    {
        Pets.TryGetPet(PET, out var pet).Should().BeTrue();

        return (RoomPetAvatarSnapshot)pet!.GetSnapshot();
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
                TypeId = HORSE,
                PaletteId = 3,
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
            HasSaddle = true,
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
