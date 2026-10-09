using System.Collections;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A roller carrying a furni into or out of a switched-on Room Area Hider's area: the furni
/// follows the rule a player's move and a wired move follow (hidden furni are not sent), so it
/// is taken away when it rolls in, left out of the slide while hidden, and sent again when it
/// rolls out. Before, the slide packet carried it either way and the clients showed it inside
/// the hidden area, or never got it back out of it.
/// </summary>
public sealed class RollerAreaHideTests
{
    private const int HIDER = 40;
    private const int ROLLER_IN = 41;
    private const int ROLLER_INSIDE = 42;
    private const int ROLLER_OUT = 43;
    private const int CRATE = 44;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext Owner => ActionContext.CreateForSystem(1);

    private RoomRollerSystem Rollers => _room.Harness.Module<RoomRollerSystem>();

    [Fact]
    public async Task A_furni_rolled_into_a_hidden_area_is_hidden_and_shown_again_when_rolled_out()
    {
        // Three rollers heading east along y = 3: from x = 1 into the area (x 2..3), inside it, and out.
        var hider = _room.AddFloorItem(
            HIDER,
            7,
            7,
            "area_hide",
            createLogic: (factory, ctx) => new FurnitureAreaHideLogic(factory, ctx)
        );
        AddRoller(ROLLER_IN, 1, 3);
        AddRoller(ROLLER_INSIDE, 2, 3);
        AddRoller(ROLLER_OUT, 3, 3);
        var crate = _room.AddFloorItem(CRATE, 1, 3);
        crate.SetPositionZ(Altitude.FromInt(100));

        await hider.Logic.OnInteractAsync(
            Owner,
            new SetAreaHideInteraction
            {
                RootX = 2,
                RootY = 2,
                Width = 2,
                Length = 3,
                Invisibility = false,
                WallItems = false,
                Invert = false,
            },
            Ct
        );
        await hider.Logic.OnUseAsync(Owner, 0, Ct);
        RoomHarness.SetMember(_room.Harness.State, "IsFurniLoaded", true);
        await Rollers.OnRoomEventAsync(
            new RoomRollerChangedEvent
            {
                RoomId = 1,
                CausedBy = Owner,
                ObjectId = ROLLER_IN,
            },
            Ct
        );

        // Switching the hider on took the two rollers inside the area away, as it should.
        SentToRoom<ObjectRemoveMessageComposer>()
            .Select(x => x.ObjectId)
            .Should()
            .Equal((RoomObjectId)ROLLER_INSIDE, (RoomObjectId)ROLLER_OUT);
        var removedBefore = SentToRoom<ObjectRemoveMessageComposer>().Count();

        await TickAsync();

        crate.X.Should().Be(2);
        SlidFurni().Should().BeEmpty("the crate rolled out of sight");
        SentToRoom<ObjectRemoveMessageComposer>()
            .Skip(removedBefore)
            .Select(x => x.ObjectId)
            .Should()
            .Equal((RoomObjectId)CRATE);

        await TickAsync();

        crate.X.Should().Be(3);
        SlidFurni().Should().BeEmpty("it stays out of sight");

        await TickAsync();

        crate.X.Should().Be(4);
        SlidFurni().Should().BeEmpty("it is sent again instead");
        SentToRoom<ObjectAddMessageComposer>()
            .Select(x => x.FloorItem.ObjectId)
            .Should()
            .Equal((RoomObjectId)CRATE);
    }

    private void AddRoller(int id, int x, int y)
    {
        var roller = _room.AddFloorItem(
            id,
            x,
            y,
            "roller",
            createLogic: (factory, ctx) => new FurnitureRollerLogic(factory, ctx)
        );

        roller.SetRotation(Rotation.East);
    }

    private async Task TickAsync()
    {
        _now += 2_000;
        await Rollers.ProcessRollersAsync(_now, Ct);
    }

    private IEnumerable<int> SlidFurni() =>
        SentToRoom<SlideObjectBundleMessageComposer>()
            .SelectMany(x => x.FloorItemHeights)
            .Select(x => x.Item1);

    private IEnumerable<T> SentToRoom<T>() =>
        _room
            .Harness.Fakes.Log.Calls.SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<T>();
}
