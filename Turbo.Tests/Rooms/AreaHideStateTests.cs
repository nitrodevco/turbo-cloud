using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A Room Area Hider switched on by anything but its own click: wired setting its state, or the
/// hider placed again while still on. The clients must be told the area and lose the furni in it,
/// as when it is clicked; before, only the server hid them, and the clients drew furni that then
/// never moved for them.
/// </summary>
public sealed class AreaHideStateTests
{
    private const int HIDER = 40;
    private const int CRATE = 44;

    private readonly WiredRoom _room = new(8, 8);
    private readonly IRoomFloorItem _hider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext Owner => ActionContext.CreateForSystem(1);

    public AreaHideStateTests()
    {
        _hider = _room.AddFloorItem(
            HIDER,
            7,
            7,
            "area_hide",
            createLogic: (factory, ctx) => new FurnitureAreaHideLogic(factory, ctx)
        );
        _room.AddFloorItem(CRATE, 2, 3);
    }

    private Task SetAreaAsync() =>
        _hider.Logic.OnInteractAsync(
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

    [Fact]
    public async Task Wired_switching_it_on_tells_the_room_and_takes_away_what_it_hides()
    {
        await SetAreaAsync();
        var sentBefore = SentCount();

        // As wired's toggle and match-to-snapshot set it (WiredExecutionContext).
        await _hider.Logic.SetStateAsync(1, false);

        SentToRoom<AreaHideMessageComposer>()
            .Skip(sentBefore.Areas)
            .Should()
            .ContainSingle()
            .Which.AreaHideData.On.Should()
            .BeTrue();
        SentToRoom<ObjectRemoveMessageComposer>()
            .Skip(sentBefore.Removes)
            .Select(x => x.ObjectId)
            .Should()
            .Equal((RoomObjectId)CRATE);
    }

    [Fact]
    public async Task Placing_it_again_while_on_tells_the_room_and_takes_away_what_it_hides()
    {
        await SetAreaAsync();
        await _hider.Logic.OnUseAsync(Owner, 0, Ct);

        // Picked up while on, which shows the crate again, and placed back where it was.
        await _room.Harness.Module<RoomObjectModule>().RemoveObjectAsync(Owner, _hider, Ct, 1);
        var sentBefore = SentCount();

        (
            await _room
                .Harness.Module<RoomFurniModule>()
                .PlaceFloorItemAsync(Owner, _hider, 7, 7, Rotation.North, Ct)
        )
            .Should()
            .BeTrue();

        SentToRoom<AreaHideMessageComposer>()
            .Skip(sentBefore.Areas)
            .Should()
            .ContainSingle()
            .Which.AreaHideData.On.Should()
            .BeTrue();
        SentToRoom<ObjectRemoveMessageComposer>()
            .Skip(sentBefore.Removes)
            .Select(x => x.ObjectId)
            .Should()
            .Equal((RoomObjectId)CRATE);
    }

    private (int Areas, int Removes) SentCount() =>
        (
            SentToRoom<AreaHideMessageComposer>().Count(),
            SentToRoom<ObjectRemoveMessageComposer>().Count()
        );

    private IEnumerable<T> SentToRoom<T>() =>
        _room
            .Harness.Fakes.Log.Calls.SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<T>();
}
