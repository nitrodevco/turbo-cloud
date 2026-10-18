using FluentAssertions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// How high furni may be stacked. Habbo refuses a placement with
/// <c>furni_placement_error</c> / <c>${room.error.cant_set_item}</c> once the item's top would be
/// over 80 (official client capture, 2026-10-18: a 0.65 box topping out at 79.40 placed 10/10, one
/// topping out at 80.05 refused 12/12; an 8.1 pillar on a 72.9 stack, top 81.0, refused 2/2). The
/// stacking helper's window goes up to the same 80 (WIN63-202610151745
/// <c>CustomStackHeightWidget.as:27</c>, <c>MAX_HEIGHT = 80</c>).
/// </summary>
public sealed class MaxStackHeightTests
{
    private const int BASE = 30;
    private int _nextItem = 31;

    private readonly WiredRoom _room = new(8, 8);

    /// <summary>A stack whose top is <paramref name="top"/> on (2, 2).</summary>
    private void StackTo(double top)
    {
        var block = _room.AddFloorItem(BASE, 2, 2, canWalk: false);

        block.SetPositionZ(Altitude.FromValue(top - block.Definition.StackHeight.Value));
        _room.Map.ComputeTile(2, 2);
    }

    private bool CanPlace(double stackHeight)
    {
        var item = _room.AddFloorItem(_nextItem++, 5, 5, canWalk: false);

        RoomHarness.SetMember(
            item,
            "Definition",
            item.Definition with
            {
                StackHeight = Altitude.FromValue(stackHeight),
            }
        );

        return _room
            .Harness.Module<RoomFurniModule>()
            .CanPlaceFloorItem(item, 2, 2, Rotation.North);
    }

    [Fact]
    public void Stacks_go_up_to_80_by_default() =>
        new RoomConfig().MaxStackHeight.Value.Should().Be(80);

    [Fact]
    public void An_item_whose_top_stays_under_80_is_placed()
    {
        StackTo(78.75);

        CanPlace(0.65).Should().BeTrue("Habbo placed a box topping out at 79.40");
    }

    [Fact]
    public void An_item_whose_top_would_pass_80_is_refused()
    {
        StackTo(79.40);

        CanPlace(0.65).Should().BeFalse("Habbo refused a box topping out at 80.05");
    }

    [Fact]
    public void It_is_the_items_top_that_counts_not_where_it_stands()
    {
        StackTo(72.9);

        CanPlace(8.1).Should().BeFalse("Habbo refused a pillar on 72.9 that would top out at 81");
        CanPlace(0.65).Should().BeTrue("and placed a box on the same 72.9");
    }
}
