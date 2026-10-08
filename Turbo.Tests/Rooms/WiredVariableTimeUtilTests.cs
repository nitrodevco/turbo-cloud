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
/// "Time Utilities" on a global variable holding 1 700 000 000 (2023-11-14 22:13:20 UTC), read as
/// a unix timestamp ("Value" mode). The editor ticks sub-variable <c>id</c> as bit <c>id</c> of the
/// mask (<c>SubVariableCreatorPreset</c>): 10 is "year", 21 "second", 24 "day". The advanced ones
/// count whole units since 1970 (<c>time_util.advanced_info</c>), so two of them subtract to the
/// time between two instants (baikal, "Habbo Wired: Time Tracking System").
/// </summary>
public sealed class WiredVariableTimeUtilTests
{
    private const int TIMESTAMP = 1_700_000_000;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(10, "year", 2023)]
    [InlineData(21, "second", TIMESTAMP)]
    [InlineData(22, "minute", TIMESTAMP / 60)]
    [InlineData(24, "day", TIMESTAMP / 86_400)]
    [InlineData(26, "month", (2023 - 1970) * 12 + 10)]
    public async Task A_ticked_sub_variable_reads_its_unit_of_the_timestamp(
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
