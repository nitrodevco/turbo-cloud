using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Which times a variable made by a box can be read. Habbo lists every furni, user and context
/// variable with <c>canReadCreationTime</c> set and <c>canReadLastUpdateTime</c> equal to
/// <c>hasValue</c> (official client capture, WiredAllVariablesDiffs / WiredFurni*: user "hp"
/// 2026-10-13T11-17-48, user without value 2026-10-24T15-54-29, furni with value
/// 2026-10-20T20-35-01, furni without 2026-10-20T20-33-04, context "ctx" 2026-10-14T09-41-56,
/// context without value "sc" 2026-10-24T14-41-42). The Variable Age editor greys its
/// "Last update time" option out for a variable without it (conditions/VariableAge.as:91-105,
/// WIN63-202610201032); ours never set it, so a tester could not pick it for a user variable.
/// </summary>
public sealed class WiredVariableTimestampFlagsTests
{
    private const int BOX = 1;

    private readonly WiredRoom _room = new(8, 8);

    [Theory]
    [InlineData("wf_var_user", 0, 1, true)]
    [InlineData("wf_var_user", 0, 0, false)]
    [InlineData("wf_var_furni", 1, 1, true)]
    [InlineData("wf_var_furni", 1, 0, false)]
    [InlineData("wf_var_context", -1, 1, true)]
    [InlineData("wf_var_context", -1, 0, false)]
    public async Task A_box_variable_has_a_last_update_time_only_with_a_value(
        string name,
        int availability,
        int hasValue,
        bool lastUpdate
    )
    {
        FurnitureWiredVariableLogic box = name switch
        {
            "wf_var_user" => _room.AddBox<WiredVariableUser>(BOX, 0, 0, name),
            "wf_var_furni" => _room.AddBox<WiredVariableFurni>(BOX, 0, 0, name),
            _ => _room.AddBox<WiredVariableContext>(BOX, 0, 0, name),
        };
        int[] ints = name switch
        {
            "wf_var_user" => [availability, hasValue],
            "wf_var_furni" => [hasValue, availability],
            _ => [hasValue],
        };

        (
            await _room.SaveWithResultAsync<UpdateVariableMessage>(
                BOX,
                intParams: ints,
                stringParam: "attempts"
            )
        )
            .IsSaved.Should()
            .BeTrue();
        await box.LoadWiredAsync(TestContext.Current.CancellationToken);

        var flags = box.GetVarSnapshot().Flags;

        flags.Has(WiredVariableFlags.CanReadCreationTime).Should().BeTrue();
        flags.Has(WiredVariableFlags.CanReadLastUpdateTime).Should().Be(lastUpdate);
    }
}
