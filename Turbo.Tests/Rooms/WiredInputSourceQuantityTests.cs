using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Input Source Quantity counts the furni or the users of its input, as the editor's merged
/// source picker says (int param 0: 1 users, 0 furni; AS3 1318 conditions/_-42v.as:54-72),
/// not both together.
/// </summary>
public sealed class WiredInputSourceQuantityTests
{
    private const int PLAYER = 5;

    private readonly WiredRoom _room = new(8, 8);

    [Theory]
    [InlineData(0, 2, true)]
    [InlineData(0, 3, false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 3, false)]
    public async Task Only_the_chosen_kind_is_counted(int countUsers, int amount, bool passes)
    {
        _room.Enter(PLAYER, 1, 1);
        _room.AddFloorItem(30, 3, 3);
        _room.AddFloorItem(31, 4, 4);

        var condition = _room.AddBox<WiredConditionSelectQuantity>(1, 0, 0, "wf_cnd_slc_quantity");

        // Equal to the amount.
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                1,
                intParams: [countUsers, amount, 1],
                furniSources:
                [
                    [WiredFurniSourceType.SelectorItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.SelectorUsers],
                ]
            )
        )
            .Should()
            .BeTrue();

        var ctx = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = Turbo.Primitives.Action.ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
            SelectorPool = new WiredSelectionSet
            {
                SelectedFurniIds = { 30, 31 },
                SelectedAvatarIds = { PLAYER },
            },
        };

        condition.Evaluate(ctx).Should().Be(passes);
    }
}
