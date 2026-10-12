using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// What a fresh box opens with. Habbo's open payloads for never-saved boxes (official client
/// capture, WIN63-202610121709, intParams and defaultIntParams alike): Give Variable and Change
/// Variable [1,0,0,0,0,-10,0], Variable Age [1,0,0,0,0,1,0,-10] - the reference is a global
/// variable (-10) and Variable Age compares "less than" (0). Ours opened the reference on users
/// (1) and Variable Age on "more than" (2). A box that was saved keeps what it stored.
/// </summary>
public sealed class WiredReferenceTargetDefaultTests
{
    private const int BOX = 2;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_fresh_give_variable_opens_as_habbo_s()
    {
        var give = _room.AddBox<WiredActionGiveVariable>(BOX, 0, 0, "wf_act_give_var");

        (await OpenedWith(give)).Should().Equal(1, 0, 0, 0, 0, -10, 0);
    }

    [Fact]
    public async Task A_fresh_change_variable_opens_as_habbo_s()
    {
        var change = _room.AddBox<WiredActionChangeVariable>(BOX, 0, 0, "wf_act_change_var_val");

        (await OpenedWith(change)).Should().Equal(1, 0, 0, 0, 0, -10, 0);
    }

    [Fact]
    public async Task A_fresh_variable_age_opens_as_habbo_s()
    {
        var age = _room.AddBox<WiredConditionVariableAgeMatch>(BOX, 0, 0, "wf_cnd_var_age_match");

        (await OpenedWith(age)).Should().Equal(1, 0, 0, 0, 0, 1, 0, -10);
    }

    [Fact]
    public async Task A_saved_box_keeps_its_reference_target()
    {
        var give = _room.AddBox<WiredActionGiveVariable>(BOX, 0, 0, "wf_act_give_var");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams:
                [
                    (int)WiredVariableTargetType.User,
                    0,
                    0,
                    0,
                    1,
                    (int)WiredVariableTargetType.User,
                    0,
                ],
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();
        await give.LoadWiredAsync(Ct);

        give.GetSnapshot().IntParams.Should().Equal(1, 0, 0, 0, 1, 1, 0);
    }

    /// <summary>The int params and the defaults the editor gets for a box that was never saved.</summary>
    private static async Task<List<int>> OpenedWith(FurnitureWiredLogic box)
    {
        await box.LoadWiredAsync(Ct);

        var snapshot = box.GetSnapshot();

        snapshot.DefaultIntParams.Should().Equal(snapshot.IntParams);

        return [.. snapshot.IntParams];
    }
}
