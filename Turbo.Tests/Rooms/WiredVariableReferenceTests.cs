using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The WIN63-202610121624 variable boxes, saved with the int params their editors now write
/// (AS3 <c>readIntParamsFromForm</c> of ChangeVariable, VariableValue, WithVariable, GiveVariable,
/// VariableAge and the VariableUpdate trigger). Wired Faculty 12/10/2026: "Refer to the same
/// user/furni in variable boxes ... increase @altitude with @height on a group of furni without
/// mixing", "Give Variable can initialize from a variable", "Variable Age can use timeout from a
/// variable", "Variable Value Changed works on multiple variables ... by selecting the variable
/// boxes". Two furni hold "level" 0 and "bonus" 5 and 9.
/// </summary>
public sealed class WiredVariableReferenceTests
{
    private const int LEVEL_BOX = 10;
    private const int BONUS_BOX = 11;
    private const int BOX = 2;
    private const int FURNI_A = 30;
    private const int FURNI_B = 31;
    private const int ADD = (int)WiredVariableOperationType.Add;
    private const int FURNI = (int)WiredVariableTargetType.Furni;

    private readonly WiredRoom _room = new(8, 8);
    private WiredVariableFurni _level = null!;
    private WiredVariableFurni _bonus = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string LevelId => _level.GetVarSnapshot().VariableId.ToString();

    private string BonusId => _bonus.GetVarSnapshot().VariableId.ToString();

    [Fact]
    public async Task Change_variable_with_the_same_holder_adds_each_furni_its_own_bonus()
    {
        await BuildAsync(giveLevel: true);
        var change = _room.AddBox<WiredActionChangeVariable>(BOX, 0, 0, "wf_act_change_var_val");

        // target, operation, from variable, value (hi, lo), reference target, same holder
        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [FURNI, ADD, 1, 0, 0, FURNI, 1],
                stuffIds: [FURNI_A, FURNI_B],
                definitionSpecifics: [0],
                variableIds: [LevelId, BonusId]
            )
        )
            .Should()
            .BeTrue();

        (await change.ExecuteAsync(Context(), Ct)).Should().BeTrue();

        Level(FURNI_A).Should().Be(5);
        Level(FURNI_B).Should().Be(9);
    }

    [Fact]
    public async Task Change_variable_saved_before_the_release_keeps_its_params()
    {
        await BuildAsync(giveLevel: true);
        var change = _room.AddBox<WiredActionChangeVariable>(BOX, 0, 0, "wf_act_change_var_val");

        // Six params, as the 20260916 editor saved them: an add of 3.
        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [FURNI, ADD, 0, 0, 3, FURNI],
                stuffIds: [FURNI_A, FURNI_B],
                definitionSpecifics: [0],
                variableIds: [LevelId]
            )
        )
            .Should()
            .BeTrue();
        await change.LoadWiredAsync(Ct);

        (await change.ExecuteAsync(Context(), Ct)).Should().BeTrue();

        Level(FURNI_A).Should().Be(3);
        Level(FURNI_B).Should().Be(3);
    }

    [Fact]
    public async Task Give_variable_initializes_from_the_same_holders_variable()
    {
        await BuildAsync(giveLevel: false);
        var give = _room.AddBox<WiredActionGiveVariable>(BOX, 0, 0, "wf_act_give_var");

        // target, value (hi, lo), override, from variable, reference target, same holder
        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [FURNI, 0, 0, 0, 1, FURNI, 1],
                stuffIds: [FURNI_A, FURNI_B],
                definitionSpecifics: [0],
                variableIds: [LevelId, BonusId]
            )
        )
            .Should()
            .BeTrue();

        (await give.ExecuteAsync(Context(), Ct)).Should().BeTrue();

        Level(FURNI_A).Should().Be(5);
        Level(FURNI_B).Should().Be(9);
    }

    [Fact]
    public async Task Variable_value_with_the_same_holder_compares_each_furni_with_itself()
    {
        await BuildAsync(giveLevel: false);
        await GiveAsync(_level, FURNI_A, 5);
        await GiveAsync(_level, FURNI_B, 9);
        var condition = _room.AddBox<WiredConditionVariableValueMatch>(
            BOX,
            0,
            0,
            "wf_cnd_var_val_match"
        );

        // target, comparison (equals), from variable, value (hi, lo), reference target, same holder
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [FURNI, (int)WiredComparisonType.Equals, 1, 0, 0, FURNI, 1],
                definitionSpecifics: [0],
                variableIds: [LevelId, BonusId]
            )
        )
            .Should()
            .BeTrue();

        var ctx = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = StackChanged(),
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };
        ctx.Selected.SelectedFurniIds.UnionWith([FURNI_A, FURNI_B]);

        condition.Evaluate(ctx).Should().BeTrue();
    }

    [Fact]
    public async Task Furni_with_variable_compares_each_holder_with_its_own_reference()
    {
        await BuildAsync(giveLevel: false);
        await GiveAsync(_level, FURNI_A, 5);
        await GiveAsync(_level, FURNI_B, 1);
        var selector = _room.AddBox<WiredSelectorItemsWithVariable>(
            BOX,
            0,
            0,
            "wf_slc_furni_with_var"
        );

        // comparison (equals), reference 2 = a variable, value (hi, lo), reference target, same holder
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                BOX,
                intParams: [(int)WiredComparisonType.Equals, 2, 0, 0, FURNI, 1],
                definitionSpecifics: [false, false],
                variableIds: [LevelId, BonusId]
            )
        )
            .Should()
            .BeTrue();

        var ctx = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = StackChanged(),
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };

        var picked = await selector.SelectAsync(ctx, Ct);

        picked.SelectedFurniIds.Should().Equal(FURNI_A);
    }

    [Fact]
    public async Task Variable_age_reads_its_duration_from_a_variable()
    {
        await BuildAsync(giveLevel: true);
        var condition = _room.AddBox<WiredConditionVariableAgeMatch>(
            BOX,
            0,
            0,
            "wf_cnd_var_age_match"
        );

        // target, comparison (0 less), creation time, duration (hi, lo) 0, unit seconds,
        // from variable, reference target: "level" was given under "bonus" (5 and 9) seconds ago.
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [FURNI, 0, 0, 0, 0, (int)WiredTimeUnitType.Seconds, 1, FURNI],
                definitionSpecifics: [0],
                variableIds: [LevelId, BonusId]
            )
        )
            .Should()
            .BeTrue();

        var ctx = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = StackChanged(),
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };
        ctx.Selected.SelectedFurniIds.UnionWith([FURNI_A, FURNI_B]);

        condition.Evaluate(ctx).Should().BeTrue();
    }

    [Fact]
    public async Task Variable_changed_watches_the_picked_variable_boxes()
    {
        await BuildAsync(giveLevel: true);
        var trigger = _room.AddBox<WiredTriggerVariableChanged>(BOX, 0, 0, "wf_trg_var_changed");

        // created, changed, deleted, kinds (all), origins (all), mode 1 = picked variable boxes
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                BOX,
                intParams: [0, 1, 0, 0, -1, 1],
                stuffIds: [BONUS_BOX]
            )
        )
            .Should()
            .BeTrue();

        (await trigger.MatchesEventAsync(Changed(_bonus), Ct)).Should().BeTrue();
        (await trigger.MatchesEventAsync(Changed(_level), Ct)).Should().BeFalse();
    }

    private async Task BuildAsync(bool giveLevel)
    {
        _room.AddFloorItem(FURNI_A, 1, 5);
        _room.AddFloorItem(FURNI_B, 2, 5);
        _level = _room.AddBox<WiredVariableFurni>(LEVEL_BOX, 4, 4, "wf_var_furni");
        _bonus = _room.AddBox<WiredVariableFurni>(BONUS_BOX, 5, 4, "wf_var_furni");

        foreach (var (box, name) in new[] { (LEVEL_BOX, "level"), (BONUS_BOX, "bonus") })
            (
                await _room.SaveAsync<UpdateVariableMessage>(
                    box,
                    intParams: [(int)WiredAvailabilityType.RoomActive, 1],
                    stringParam: name
                )
            )
                .Should()
                .BeTrue();

        var wired = _room.Harness.Module<RoomWiredSystem>();

        await wired.OnRoomEventAsync(
            new WiredVariableBoxChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                BoxIds = [LEVEL_BOX, BONUS_BOX],
            },
            Ct
        );
        await wired.ProcessWiredAsync(11_000, dormant: false, Ct);

        await GiveAsync(_bonus, FURNI_A, 5);
        await GiveAsync(_bonus, FURNI_B, 9);

        if (giveLevel)
        {
            await GiveAsync(_level, FURNI_A, 0);
            await GiveAsync(_level, FURNI_B, 0);
        }
    }

    private static async Task GiveAsync(WiredVariableFurni variable, int furni, int value) =>
        (
            await variable.GiveValueAsync(
                new WiredVariableKey(
                    variable.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.Furni,
                    furni
                ),
                value,
                true
            )
        )
            .Should()
            .BeTrue();

    private long? Level(int furni) =>
        _level.TryGetValue(
            new WiredVariableKey(
                _level.GetVarSnapshot().VariableId,
                WiredVariableTargetType.Furni,
                furni
            ),
            out var value
        )
            ? value.Value
            : null;

    private WiredExecutionContext Context()
    {
        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedFurniIds.UnionWith([FURNI_A, FURNI_B]);

        return ctx;
    }

    private static RoomWiredStackChangedEvent StackChanged() =>
        new()
        {
            RoomId = 1,
            CausedBy = ActionContext.CreateForSystem(1),
            StackIds = [],
        };

    private static WiredVariableChangedEvent Changed(WiredVariableFurni variable) =>
        new()
        {
            RoomId = 1,
            CausedBy = ActionContext.CreateForSystem(1),
            VariableId = variable.GetVarSnapshot().VariableId,
            TargetType = WiredVariableTargetType.Furni,
            TargetId = FURNI_A,
            ChangeType = WiredVariableChangeType.Updated,
            Value = new WiredVariableValue(6),
            PreviousValue = new WiredVariableValue(5),
        };
}
