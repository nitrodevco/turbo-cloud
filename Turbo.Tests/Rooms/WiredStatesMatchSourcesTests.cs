using FluentAssertions;
using Turbo.Primitives.Furniture.ExtraData;
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
/// States Match compares the furni of its source with the box's snapshot (official client
/// capture of the box's sources, [0,100,200,201] default 100): all picked furni by default, or
/// only the triggering, selector or signal furni among them. Lamp 30 stands as saved, lamp 31
/// was saved one tile further.
/// </summary>
public sealed class WiredStatesMatchSourcesTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Theory]
    [InlineData(WiredFurniSourceType.SelectedItems, false)]
    [InlineData(WiredFurniSourceType.TriggeredItem, true)]
    public async Task Only_the_source_furni_are_compared(WiredFurniSourceType source, bool passes)
    {
        _room.AddFloorItem(30, 3, 3);
        _room.AddFloorItem(31, 4, 4);

        var condition = _room.AddBox<WiredConditionItemMatches>(1, 0, 0, "wf_cnd_match_snapshot");

        // Position only.
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                1,
                intParams: [0, 0, 1, 0],
                stuffIds: [30, 31],
                furniSources:
                [
                    [source],
                ]
            )
        )
            .Should()
            .BeTrue();

        _room
            .FloorItem(1)
            .ExtraData.UpdateSection(
                WiredFurniSnapshotEntry.SECTION,
                new Dictionary<int, WiredFurniSnapshotEntry>
                {
                    [30] = new(0, 0, 3, 3, 0),
                    [31] = new(0, 0, 5, 4, 0),
                }
            );

        var ctx = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = Turbo.Primitives.Action.ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
            Selected = new WiredSelectionSet { SelectedFurniIds = { 30 } },
        };

        condition.Evaluate(ctx).Should().Be(passes);
    }
}
