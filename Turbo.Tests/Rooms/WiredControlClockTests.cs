using FluentAssertions;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Control clock" commands 3 and 4 are the editor's "Pause" and "Resume"
/// (<c>wiredfurni.params.clock_control.3</c> / <c>.4</c>): pausing holds the time, resuming runs on
/// from it. The clock is running at 10 seconds.
/// </summary>
public sealed class WiredControlClockTests
{
    private const int CLOCK = 30;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pause_holds_the_time_and_resume_runs_on_from_it()
    {
        var clock = (FurnitureCounterClockLogic)
            _room
                .AddFloorItem(
                    CLOCK,
                    3,
                    3,
                    "wf_game_upcounter",
                    createLogic: (factory, ctx) => new FurnitureCounterClockLogic(factory, ctx)
                )
                .Logic;

        await clock.AdjustAsync(WiredOperatorType.Set, 20, Ct);
        await clock.ControlAsync(WiredClockControlType.Start, Ct);

        await clock.ControlAsync((WiredClockControlType)3, Ct);

        clock.IsRunning.Should().BeFalse();
        clock.GetState().Should().Be(10);

        await clock.ControlAsync((WiredClockControlType)4, Ct);

        clock.IsRunning.Should().BeTrue();
        clock.GetState().Should().Be(10);
    }
}
