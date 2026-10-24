using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Time Utilities" on a global variable holding 90 061, read as a unix timestamp in MILLISECONDS
/// ("Value" mode): 1970-01-01 00:01:30.061 UTC, a Thursday (official client capture 2026-10-24,
/// WIN63-202610201032, wired matrix addon.1002.R1, 10/10: millisecond 90061, second 90, minute 1,
/// hour 0, day 0, seconds_of_minute 30, minute_of_hour 1, day_of_week 4, year 1970; ours read
/// seconds). The editor ticks sub-variable <c>id</c> as bit <c>id</c> of the
/// mask (<c>SubVariableCreatorPreset</c>): 10 is "year", 21 "second", 24 "day". The advanced ones
/// count whole units since 1970 (<c>time_util.advanced_info</c>), so two of them subtract to the
/// time between two instants (baikal, "Habbo Wired: Time Tracking System").
/// </summary>
public sealed class WiredVariableTimeUtilTests
{
    private const int TIMESTAMP = 90_061;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(1, "milliseconds_of_seconds", 61)]
    [InlineData(2, "seconds_of_minute", 30)]
    [InlineData(3, "minute_of_hour", 1)]
    [InlineData(5, "day_of_week", 4)]
    [InlineData(10, "year", 1970)]
    [InlineData(20, "millisecond", 90_061)]
    [InlineData(21, "second", 90)]
    [InlineData(22, "minute", 1)]
    [InlineData(23, "hour", 0)]
    [InlineData(24, "day", 0)]
    public async Task Addon_1002_R1_a_ticked_sub_variable_reads_the_value_as_epoch_milliseconds(
        int id,
        string name,
        int expected
    )
    {
        var global = _room.AddBox<WiredVariableRoom>(10, 4, 4, "wf_var_room");
        var addon = _room.AddBox<WiredAddonVariableTimeUtil>(11, 4, 4, "wf_xtra_var_time_util");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                10,
                intParams: [(int)WiredAvailabilityType.RoomActive],
                stringParam: "stamp"
            )
        )
            .Should()
            .BeTrue();
        (await _room.SaveAsync<UpdateAddonMessage>(11, intParams: [1 << id, 0])).Should().BeTrue();
        await global.LoadWiredAsync(Ct);

        var key = new WiredVariableKey(
            global.GetVarSnapshot().VariableId,
            WiredVariableTargetType.Global,
            0
        );
        (
            await global.SetValueAsync(
                _room.Harness.Fakes.Create<IWiredExecutionContext>(),
                key,
                TIMESTAMP
            )
        )
            .Should()
            .BeTrue();

        var sub = addon.GetSubVariables().Should().ContainSingle().Subject;

        sub.GetVarSnapshot().VariableName.Should().Be($"stamp.{name}");
        sub.TryGetValue(key with { VariableId = sub.GetVarSnapshot().VariableId }, out var value)
            .Should()
            .BeTrue();
        ((int)value).Should().Be(expected);
    }
}
