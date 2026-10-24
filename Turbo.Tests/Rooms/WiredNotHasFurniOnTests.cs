using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Furni doesn't have furni on" with the option its editor saves (AS3 <c>DontHaveStackedFurnis</c>:
/// 0 <c>not_requireall.0</c> "If one or more of the selected Furni has NO Furni on them", 1
/// <c>not_requireall.1</c> "All the selected Furni have NO Furni on them"). Of the two picked furni,
/// 30 has a furni on it and 31 has none.
/// </summary>
public sealed class WiredNotHasFurniOnTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task The_option_chooses_one_or_all_of_the_picked_furni(int option, bool holds)
    {
        _room.AddFloorItem(30, 2, 5);
        _room.AddFloorItem(31, 4, 5);
        var onTop = _room.AddFloorItem(32, 2, 5);
        onTop.UseZ(Altitude.FromInt(100));

        var condition = _room.AddBox<WiredNegativeConditionItemHasItems>(
            2,
            0,
            0,
            "wf_cnd_not_furni_on"
        );
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                2,
                intParams: [option],
                stuffIds: [30, 31],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        var ctx = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };

        condition.Evaluate(ctx).Should().Be(holds);
    }

    // condition.7.R1 / condition.18.R1 (4/4 each) and condition.7.B3 / condition.18.B3 (2/2 each):
    // with nothing picked both boxes fail, the negative one too (official client capture
    // 2026-10-24T19-47-24, W1 lab); an avatar standing on the picked furni (a bot in the lab) is
    // no stacked furni (2026-10-24T20-03-05).
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public async Task No_picks_fail_both_boxes_and_a_bot_on_the_furni_is_not_stacked(
        bool negative,
        bool picked,
        bool holds
    )
    {
        _room.AddFloorItem(30, 2, 5);
        _room.Enter(40, 2, 5);

        var condition = negative
            ? _room.AddBox<WiredNegativeConditionItemHasItems>(2, 0, 0, "wf_cnd_not_furni_on")
            : _room.AddBox<WiredConditionItemHasItems>(2, 0, 0, "wf_cnd_has_furni_on");
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                2,
                intParams: [0],
                stuffIds: picked ? [30] : [],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        condition.Evaluate(NewContext()).Should().Be(holds);
    }

    private WiredProcessingContext NewContext() =>
        new(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };
}
