using FluentAssertions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// What a never-saved box opens with, against the defaultIntParams Habbo sends for the same box
/// (official client capture, WIN63-202610131318 / 202610121709): User Says Keyword [0,0,1] (hide
/// the message), Move To Direction [0,0,1] (blocked by users), Movement Physics [1,0,0,0] (keep
/// altitude), Variable Changed [1,1,0,7,-1,0], Variable Value [1,2,0,0,0,-10,0], Users With
/// Variable [2,0,0,0,-10,0] Place Furni [0 x9, -20] and Initiate Transaction [0,10,0,-20,0,300]. Ours opened every one of them with the
/// ticks off, "equals" and the reference on users.
/// </summary>
public sealed class WiredFreshBoxDefaultsTests
{
    private const int BOX = 2;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_fresh_says_keyword_hides_the_message() =>
        (
            await OpenedWith(
                _room.AddBox<WiredTriggerHabboSaysKeyword>(BOX, 0, 0, "wf_trg_says_something")
            )
        )
            .Should()
            .Equal(0, 0, 1);

    [Fact]
    public async Task A_fresh_move_to_direction_is_blocked_by_users() =>
        (
            await OpenedWith(
                _room.AddBox<WiredActionMoveToDirection>(BOX, 0, 0, "wf_act_move_to_dir")
            )
        )
            .Should()
            .Equal(0, 0, 1);

    [Fact]
    public async Task A_fresh_movement_physics_keeps_altitude() =>
        (
            await OpenedWith(
                _room.AddBox<WiredAddonMovementPhysics>(BOX, 0, 0, "wf_xtra_mov_physics")
            )
        )
            .Should()
            .Equal(1, 0, 0, 0);

    [Fact]
    public async Task A_fresh_variable_changed_fires_on_created_and_every_change() =>
        (
            await OpenedWith(
                _room.AddBox<WiredTriggerVariableChanged>(BOX, 0, 0, "wf_trg_var_changed")
            )
        )
            .Should()
            .Equal(1, 1, 0, 7, -1, 0);

    [Fact]
    public async Task A_fresh_variable_value_compares_more_than_a_global_reference() =>
        (
            await OpenedWith(
                _room.AddBox<WiredConditionVariableValueMatch>(BOX, 0, 0, "wf_cnd_var_val_match")
            )
        )
            .Should()
            .Equal(1, 2, 0, 0, 0, -10, 0);

    [Fact]
    public async Task A_fresh_users_with_variable_references_a_global() =>
        (
            await OpenedWith(
                _room.AddBox<WiredSelectorEntitiesWithVariable>(BOX, 0, 0, "wf_slc_users_with_var")
            )
        )
            .Should()
            .Equal(2, 0, 0, 0, -10, 0);

    [Fact]
    public async Task A_fresh_place_furni_reads_a_context_value() =>
        (await OpenedWith(_room.AddBox<WiredActionPlaceFurni>(BOX, 0, 0, "wf_act_place_furni")))
            .Should()
            .Equal(0, 0, 0, 0, 0, 0, 0, 0, 0, -20);

    [Fact]
    public async Task A_fresh_initiate_transaction_asks_ten_from_a_context_value() =>
        (
            await OpenedWith(
                _room.AddBox<WiredActionInitiateTransaction>(BOX, 0, 0, "wf_act_init_transaction")
            )
        )
            .Should()
            .Equal(0, 10, 0, -20, 0, 300);

    /// <summary>The int params and the defaults the editor gets for a box that was never saved.</summary>
    private static async Task<List<int>> OpenedWith(FurnitureWiredLogic box)
    {
        await box.LoadWiredAsync(Ct);

        var snapshot = box.GetSnapshot();

        snapshot.DefaultIntParams.Should().Equal(snapshot.IntParams);

        return [.. snapshot.IntParams];
    }
}
