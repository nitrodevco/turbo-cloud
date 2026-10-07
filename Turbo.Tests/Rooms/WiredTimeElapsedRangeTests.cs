using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Time elapsed is less / more than": Flash's slider runs 1 to 1200 half seconds and the box saves
/// it plus one pulse (onEditStart shows pulses - 1), so the editor's longest time is 1201.
/// </summary>
public sealed class WiredTimeElapsedRangeTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public async Task Less_than_keeps_the_longest_time_the_editor_saves()
    {
        var box = _room.AddBox<WiredConditionTimerLessThan>(1, 0, 0, "wf_cnd_time_less_than");

        (await _room.SaveAsync<UpdateConditionMessage>(1, intParams: [1201])).Should().BeTrue();

        box.GetSnapshot().IntParams.Should().Equal(1201);
    }

    [Fact]
    public async Task More_than_keeps_the_longest_time_the_editor_saves()
    {
        var box = _room.AddBox<WiredConditionTimerMoreThan>(1, 0, 0, "wf_cnd_time_more_than");

        (await _room.SaveAsync<UpdateConditionMessage>(1, intParams: [1201])).Should().BeTrue();

        box.GetSnapshot().IntParams.Should().Equal(1201);
    }
}
