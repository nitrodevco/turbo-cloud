using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The fields of the variable boxes (wired matrix family W5, case ids
/// <c>&lt;holder&gt;.&lt;code&gt;.E&lt;n&gt;</c>): every value the official editor can send is saved,
/// and one it does not offer is refused. The editors are cited per case (AS3 WIN63-202610201032,
/// paths under roomevents/); how the official server answers a value no editor sends is not known,
/// so the refusals are ours.
/// </summary>
public sealed class WiredVariableBoxFieldsTests
{
    private const int BOX = 1;
    private const int MIN = int.MinValue;
    private const int MAX = int.MaxValue;

    private readonly WiredRoom _room = new(8, 8);

    public static TheoryData<string, Type, string, int[], bool> Fields =>
        new()
        {
            // action.39.E9-E12: target 0/1/-20 (merged source options, no global), the initial
            // value as a long (Util.pushIntAsLong: -1 then a negative value), override 0/1
            // (actiontypes/GiveVariable.as:43-52).
            {
                "action.39.E9",
                typeof(WiredActionGiveVariable),
                "wf_act_give_var",
                [-20, -1, MIN, 1, 0, -10, 0],
                true
            },
            {
                "action.39.E11",
                typeof(WiredActionGiveVariable),
                "wf_act_give_var",
                [0, 0, MAX, 0, 1, 1, 1],
                true
            },
            {
                "action.39.E9",
                typeof(WiredActionGiveVariable),
                "wf_act_give_var",
                [-10, 0, 0, 0, 0, -10, 0],
                false
            },
            // action.40.E9: target 0/1/-20 (actiontypes/RemoveVariable.as).
            {
                "action.40.E9",
                typeof(WiredActionRemoveVariable),
                "wf_act_remove_var",
                [-20],
                true
            },
            { "action.40.E9", typeof(WiredActionRemoveVariable), "wf_act_remove_var", [0], true },
            // action.41.E9-E14: every operator of the dropdown incl. 111..122, operand kind 0/1,
            // the operand as a long, operand target (actiontypes/ChangeVariable.as:50-69,98-107).
            {
                "action.41.E10",
                typeof(WiredActionChangeVariable),
                "wf_act_change_var_val",
                [-10, 122, 1, -1, MIN, -20, 1],
                true
            },
            {
                "action.41.E10",
                typeof(WiredActionChangeVariable),
                "wf_act_change_var_val",
                [-20, 110, 0, 0, MAX, -10, 0],
                true
            },
            {
                "action.41.E10",
                typeof(WiredActionChangeVariable),
                "wf_act_change_var_val",
                [1, 7, 0, 0, 0, -10, 0],
                false
            },
            // condition.40.E9: target 0/1/-20 (conditions/HasVariable.as).
            {
                "condition.40.E9",
                typeof(WiredConditionHasVariable),
                "wf_cnd_has_var",
                [-20],
                true
            },
            // condition.42.E9-E14: comparison 0..5, the reference as a long.
            {
                "condition.42.E10",
                typeof(WiredConditionVariableValueMatch),
                "wf_cnd_var_val_match",
                [-10, 5, 1, -1, MIN, -20, 0],
                true
            },
            {
                "condition.42.E10",
                typeof(WiredConditionVariableValueMatch),
                "wf_cnd_var_val_match",
                [1, 6, 0, 0, 0, -10, 0],
                false
            },
            // condition.43.E10-E14: lower/higher than, creation/update, duration long, unit 0..7.
            {
                "condition.43.E14",
                typeof(WiredConditionVariableAgeMatch),
                "wf_cnd_var_age_match",
                [-20, 2, 1, 0, MAX, 7, 0, -10],
                true
            },
            {
                "condition.43.E14",
                typeof(WiredConditionVariableAgeMatch),
                "wf_cnd_var_age_match",
                [1, 0, 0, 0, 0, 8, 0, -10],
                false
            },
            // trigger.22.E10-E14: three checkboxes, change mask bits 0..2, origins bits 0..3 or
            // -1 for all (triggerconfs/VariableUpdate.as:159-163).
            {
                "trigger.22.E13",
                typeof(WiredTriggerVariableChanged),
                "wf_trg_var_changed",
                [1, 1, 1, 7, -1, 1],
                true
            },
            {
                "trigger.22.E14",
                typeof(WiredTriggerVariableChanged),
                "wf_trg_var_changed",
                [0, 0, 0, 0, 15, 0],
                true
            },
            {
                "trigger.22.E13",
                typeof(WiredTriggerVariableChanged),
                "wf_trg_var_changed",
                [1, 1, 1, 8, -1, 0],
                false
            },
            // addon.1001.E9-E13: sub-variable bits 0..7, mode 0..2, NumberInputParam 1..100000 and
            // 2..100000 (wired_setup/addons/VariableLvlUpAddon.as:69-77,133-151).
            {
                "addon.1001.E11",
                typeof(WiredAddonVariableLevelUp),
                "wf_xtra_var_lvlup_system",
                [255, 2, 100000, 100000, 100000],
                true
            },
            {
                "addon.1001.E12",
                typeof(WiredAddonVariableLevelUp),
                "wf_xtra_var_lvlup_system",
                [0, 1, 1, 2],
                true
            },
            {
                "addon.1001.E10",
                typeof(WiredAddonVariableLevelUp),
                "wf_xtra_var_lvlup_system",
                [0, 3],
                false
            },
            // addon.1002.E9/E10: sub-variable bits 1..10 and 20..26, time source 0..2.
            {
                "addon.1002.E9",
                typeof(WiredAddonVariableTimeUtil),
                "wf_xtra_var_time_util",
                [(1 << 27) - 2, 2],
                true
            },
            {
                "addon.1002.E10",
                typeof(WiredAddonVariableTimeUtil),
                "wf_xtra_var_time_util",
                [0, 3],
                false
            },
            // addon.1200-1205.E9-E29: the shared fx fields at the ends of their sanitize
            // (wired_setup/addons/variablefx/model/VariableFxEditorState.as:107-181): source 0/1,
            // audience 0..4, show mode 0..2, update mask 0..15, duration 1500..20000, the largest
            // colour/width/renderer ids (room/object/visualization/variablefx/
            // VariableFxIdResolver.as:32-36), min and max as longs, override targets -10 or the
            // source, segments 0..100, then the category slot and the stack order.
            {
                "addon.1200.E14",
                typeof(WiredAddonVariableFxHealthPoints),
                "wf_xtra_varfx_hp",
                Fx(category: 0),
                true
            },
            {
                "addon.1201.E14",
                typeof(WiredAddonVariableFxProgressBar),
                "wf_xtra_varfx_prog",
                Fx(category: 0),
                true
            },
            {
                "addon.1202.E33",
                typeof(WiredAddonVariableFxLevellingProgress),
                "wf_xtra_varfx_levelling",
                Fx(category: 4),
                true
            },
            {
                "addon.1203.E14",
                typeof(WiredAddonVariableFxStatusBar),
                "wf_xtra_varfx_status",
                Fx(category: 0),
                true
            },
            {
                "addon.1204.E14",
                typeof(WiredAddonVariableFxBossBar),
                "wf_xtra_varfx_boss",
                Fx(category: 0),
                true
            },
            {
                "addon.1205.E33",
                typeof(WiredAddonVariableFxNumberDisplay),
                "wf_xtra_varfx_number",
                Fx(category: 2),
                true
            },
            {
                "addon.1200.E14",
                typeof(WiredAddonVariableFxHealthPoints),
                "wf_xtra_varfx_hp",
                Fx(category: 0, at: 5, value: 1499),
                false
            },
            {
                "addon.1200.E14",
                typeof(WiredAddonVariableFxHealthPoints),
                "wf_xtra_varfx_hp",
                Fx(category: 0, at: 5, value: 20001),
                false
            },
            {
                "addon.1200.E12",
                typeof(WiredAddonVariableFxHealthPoints),
                "wf_xtra_varfx_hp",
                Fx(category: 0, at: 3, value: 16),
                false
            },
            {
                "addon.1200.E10",
                typeof(WiredAddonVariableFxHealthPoints),
                "wf_xtra_varfx_hp",
                Fx(category: 0, at: 1, value: 5),
                false
            },
            {
                "addon.1200.E11",
                typeof(WiredAddonVariableFxHealthPoints),
                "wf_xtra_varfx_hp",
                Fx(category: 0, at: 2, value: 3),
                false
            },
            {
                "addon.1200.E29",
                typeof(WiredAddonVariableFxHealthPoints),
                "wf_xtra_varfx_hp",
                Fx(category: 0, at: 20, value: 101),
                false
            },
            {
                "addon.1205.E33",
                typeof(WiredAddonVariableFxNumberDisplay),
                "wf_xtra_varfx_number",
                Fx(category: 3),
                false
            },
        };

    [Theory]
    [MemberData(nameof(Fields))]
    public async Task A_variable_box_saves_what_its_editor_sends_and_refuses_the_rest(
        string caseId,
        Type logic,
        string name,
        int[] ints,
        bool saved
    )
    {
        var box = AddBox(logic, name);

        (await SaveAsync(box, ints)).IsSaved.Should().Be(saved, caseId);

        if (saved)
            box.GetSnapshot().IntParams.Take(ints.Length).Should().Equal(ints, caseId);
    }

    /// <summary>
    /// variable.5.E13/E14, variable.6.E13/E14: the quest and quest chain boxes save
    /// "name\tquest" (<c>STRING_PARAM_SPLITTER</c>, wired_setup/variables/QuestVariable.as:17,37,
    /// QuestChainVariable.as:37) with a 500 character quest input (<c>TextInputParam("", 500)</c>,
    /// :48), and both are user variables (<c>variableType()</c> <c>USER</c>, :53-55). Ours checked
    /// the whole param as the name, so every save was refused with name_syntax, and declared no
    /// target.
    /// </summary>
    [Theory]
    [InlineData("variable.5", typeof(WiredVariableQuest), "wf_var_quest")]
    [InlineData("variable.6", typeof(WiredVariableQuestChain), "wf_var_quest_chain")]
    public async Task A_quest_variable_is_a_user_variable_named_without_its_quest(
        string caseId,
        Type logic,
        string name
    )
    {
        var box = (FurnitureWiredVariableLogic)AddBox(logic, name);
        var quest = new string('q', 500);

        (
            await _room.SaveWithResultAsync<UpdateVariableMessage>(
                BOX,
                stringParam: "my_quest\t" + quest
            )
        )
            .IsSaved.Should()
            .BeTrue(caseId);

        await box.LoadWiredAsync(TestContext.Current.CancellationToken);

        box.GetVarSnapshot().VariableName.Should().Be("my_quest", caseId);
        box.GetVarSnapshot().TargetType.Should().Be(WiredVariableTargetType.User, caseId);
        box.GetSnapshot().StringParam.Should().Be("my_quest\t" + quest, caseId);
    }

    /// <summary>
    /// variable.1.E16 (measured, wlab2 2026-10-24): a furni variable saved with availability 0
    /// ("while user in room", a user variable's) is answered with
    /// <c>wiredfurni.error.variables.invalid_availability</c>; availability 1 saves.
    /// </summary>
    [Fact]
    public async Task A_furni_variable_refuses_the_user_only_availability_variable_1_E16()
    {
        _room.AddBox<WiredVariableFurni>(BOX, 0, 0, "wf_var_furni");

        (
            await _room.SaveWithResultAsync<UpdateVariableMessage>(
                BOX,
                intParams: [1, 0],
                stringParam: "q"
            )
        )
            .ErrorKey.Should()
            .Be(WiredSaveErrors.VARIABLE_INVALID_AVAILABILITY);
        (
            await _room.SaveWithResultAsync<UpdateVariableMessage>(
                BOX,
                intParams: [1, 1],
                stringParam: "q"
            )
        )
            .IsSaved.Should()
            .BeTrue();
    }

    /// <summary>
    /// addon.1000.E9: the mapping text area takes 1000 characters (<c>TextAreaParam(..., 1000, ...)</c>,
    /// wired_setup/addons/VariableTextConnectorAddon.as); all of it is kept.
    /// </summary>
    [Fact]
    public async Task The_text_connector_keeps_a_1000_character_mapping_addon_1000_E9()
    {
        var box = AddBox(typeof(WiredAddonVariableTextConnector), "wf_xtra_var_text_connector");
        var text = "0=" + new string('t', 998);

        (await SaveAsync(box, [], text)).IsSaved.Should().BeTrue();
        box.GetSnapshot().StringParam.Should().Be(text);
    }

    /// <summary>
    /// The 23 fx ints at the ends of the editor's ranges, with <paramref name="category"/> in the
    /// category slot (21) and <paramref name="value"/> at <paramref name="at"/> when given.
    /// </summary>
    private static int[] Fx(int category, int at = -1, int value = 0)
    {
        int[] ints =
        [
            1,
            4,
            2,
            15,
            1,
            20000,
            0,
            1002,
            100,
            201,
            -1,
            MIN,
            0,
            MAX,
            1,
            1,
            -10,
            1,
            -1,
            MIN,
            100,
            category,
            MAX,
        ];

        if (at >= 0)
            ints[at] = value;

        return ints;
    }

    private FurnitureWiredLogic AddBox(Type logic, string name) =>
        (FurnitureWiredLogic)
            typeof(WiredRoom)
                .GetMethod(nameof(WiredRoom.AddBox))!
                .MakeGenericMethod(logic)
                .Invoke(_room, [BOX, 0, 0, name])!;

    private Task<WiredSaveResult> SaveAsync(
        FurnitureWiredLogic box,
        int[] ints,
        string text = ""
    ) =>
        box switch
        {
            FurnitureWiredConditionLogic => _room.SaveWithResultAsync<UpdateConditionMessage>(
                BOX,
                intParams: ints,
                stringParam: text
            ),
            FurnitureWiredTriggerLogic => _room.SaveWithResultAsync<UpdateTriggerMessage>(
                BOX,
                intParams: ints,
                stringParam: text
            ),
            FurnitureWiredAddonLogic => _room.SaveWithResultAsync<UpdateAddonMessage>(
                BOX,
                intParams: ints,
                stringParam: text
            ),
            _ => _room.SaveWithResultAsync<UpdateActionMessage>(
                BOX,
                intParams: ints,
                stringParam: text,
                definitionSpecifics: [0]
            ),
        };
}
