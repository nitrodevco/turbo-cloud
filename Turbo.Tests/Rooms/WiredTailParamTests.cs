using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Int params past a box's fixed rules, read by its tail rule. They could not be read at all: the
/// read failed and the box used its default, so "Date Range Active" (start and end are both tail
/// params) was active outside its range, and the level-up add-on ignored its step and level count.
/// </summary>
public sealed class WiredTailParamTests
{
    private const int HOUR = 3600;

    private readonly WiredRoom _room = new(8, 8);

    [Theory]
    [InlineData(HOUR, 2 * HOUR, false)]
    [InlineData(-2 * HOUR, -HOUR, false)]
    [InlineData(-HOUR, HOUR, true)]
    public async Task Date_range_active_holds_only_inside_its_range(
        int startFromNow,
        int endFromNow,
        bool holds
    )
    {
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var condition = _room.AddBox<WiredConditionDateRangeActive>(
            2,
            0,
            0,
            "wf_cnd_date_rng_active"
        );
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                2,
                intParams: [now + startFromNow, now + endFromNow],
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

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
