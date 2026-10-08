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
        RoomHarness.SetMember(onTop, "Z", Altitude.FromInt(100));

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
}
