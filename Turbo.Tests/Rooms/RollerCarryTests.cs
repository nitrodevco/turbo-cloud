using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A roller moves what is on it together: the furni and the users standing on it go to the next
/// tile on the same tick, and a user it can't take (one who doesn't roll, or can't walk there)
/// holds the furni on that roller where they are too.
/// </summary>
public sealed class RollerCarryTests
{
    private const int ROLLER = 41;
    private const int CRATE = 42;
    private const int USER = 5;

    private readonly WiredRoom _room = new(8, 8);
    private readonly IRoomFloorItem _crate;
    private readonly RoomPlayerAvatar _user;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RollerCarryTests()
    {
        var roller = _room.AddFloorItem(ROLLER, 1, 3, "roller", createLogic: (factory, ctx) =>
            new FurnitureRollerLogic(factory, ctx)
        );

        roller.SetRotation(Rotation.East);
        _crate = _room.AddFloorItem(CRATE, 1, 3);
        _crate.SetPositionZ(Altitude.FromInt(100));
        _user = _room.Enter(USER, 1, 3);
        _user.SetPositionZ(Altitude.FromInt(100));
        _room.Harness.State.IsFurniLoaded = true;
    }

    private RoomRollerSystem Rollers => _room.Harness.Module<RoomRollerSystem>();

    [Fact]
    public async Task A_roller_takes_the_furni_and_the_user_on_it_to_the_next_tile()
    {
        _room.Harness.Fakes.Handlers["CanRoll"] = _ => true;

        await RollAsync();

        _crate.X.Should().Be(2);
        _room.Positions().Should().Equal($"{USER}@2,3");
    }

    [Fact]
    public async Task A_user_the_roller_cannot_take_keeps_the_furni_on_it_where_it_is()
    {
        // The user's logic does not roll (a fake answers false).
        await RollAsync();

        _crate.X.Should().Be(1, "the roller moves its load together or not at all");
        _room.Positions().Should().Equal($"{USER}@1,3");
    }

    private async Task RollAsync()
    {
        await Rollers.OnRoomEventAsync(
            new RoomRollerChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                ObjectId = ROLLER,
            },
            Ct
        );
        await Rollers.ProcessRollersAsync(10_000, Ct);
        await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(10_000, Ct);
    }
}
