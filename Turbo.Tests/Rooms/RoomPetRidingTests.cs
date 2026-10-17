using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
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

    private readonly LiveRoomHarness _harness = new(10, 10);
    private readonly RoomPlayerAvatar _owner = new() { ObjectId = 5, PlayerId = OWNER };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomPetRidingTests()
    {
        _owner.SetPosition(3, 4);
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
            Task.FromResult<PetSnapshot?>(Horse());
    }

    private object Member(string name) => RoomHarness.GetMember(_harness.State, name)!;

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

        RiderSnapshot().Status.Should().Contain($"mv 5,4,{Altitude.FromValue(RiderHeight).ToStatusString()}");
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
