using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The fields of the chest and transaction boxes and of the bot boxes (wired matrix families W7
/// and W8, case ids <c>&lt;holder&gt;.&lt;code&gt;.E&lt;n&gt;</c>): every value the official editor
/// can send is saved as it is, and an int the editor does not offer is refused. The editors'
/// widgets are cited per case (AS3 WIN63-202610201032, paths under roomevents/); how the
/// official server answers a value no editor sends is not known, so the refusals are ours.
/// </summary>
public sealed class WiredChestBotBoxFieldsTests
{
    private const int BOX = 1;

    private static readonly int[] Targets = [0, 1, -10, -20];

    private readonly WiredRoom _room = new(8, 8);

    public static TheoryData<string, Type, string, int[], bool> Fields
    {
        get
        {
            var data = new TheoryData<string, Type, string, int[], bool>();

            // Give Currency / Give Furni From Chest (wired_setup/actiontypes/chests/_-U1N.as:110-113):
            // [rewarding mode, amount, amount is variable, amount target, show pop-up, kind's own].
            foreach (
                var (code, logic, name, kind, kindValues, kindRefused) in new (
                    string,
                    Type,
                    string,
                    int,
                    int[],
                    int[]
                )[]
                {
                    // Earnings category dropdown 11 games, 13 agency (chests/_-31f.as:46).
                    (
                        "action.45",
                        typeof(WiredActionGiveCurrencyFromChest),
                        "wf_act_give_currency",
                        11,
                        [11, 13],
                        [0, 12]
                    ),
                    // Iteration radio 0 random, 1 first in first out, 2 last in first out (chests/_-I15.as:52).
                    (
                        "action.46",
                        typeof(WiredActionGiveFurniFromChest),
                        "wf_act_give_furni",
                        0,
                        [0, 1, 2],
                        [-1, 3]
                    ),
                }
            )
            {
                int[] fresh = [0, 1, 0, -20, 1, kind];

                // E9: rewarding mode radio 0 an amount, 1 everything.
                Field(data, $"{code}.E9", logic, name, fresh, 0, [0, 1], [2]);
                // E10: the amount input, 1..2147483647.
                Field(data, $"{code}.E10", logic, name, fresh, 1, [1, int.MaxValue], [0, -1]);
                // E11: value-or-variable switch.
                Field(data, $"{code}.E11", logic, name, fresh, 2, [0, 1], [2]);
                // E12: the variable's target: furni, user (merged [[1,1]]), global, context.
                Field(data, $"{code}.E12", logic, name, fresh, 3, Targets, [-1, 2]);
                // E13: the pop-up checkbox.
                Field(data, $"{code}.E13", logic, name, fresh, 4, [0, 1], [2]);
                // E16: the kind's own field.
                Field(data, $"{code}.E16", logic, name, fresh, 5, kindValues, kindRefused);
            }

            // Initiate Transaction (chests/_-D13.as:45,93-95): [mode, multiplier, multiplier is
            // variable, multiplier target, timeout on, timeout seconds].
            var initiate = typeof(WiredActionInitiateTransaction);
            int[] initiateFresh = [0, 10, 0, -20, 0, 300];

            Field(
                data,
                "action.47.E9",
                initiate,
                "wf_act_init_transaction",
                initiateFresh,
                0,
                [0, 1, 2],
                [3]
            );
            // ValueOrVariableSection(..., 1, 500).
            Field(
                data,
                "action.47.E10",
                initiate,
                "wf_act_init_transaction",
                initiateFresh,
                1,
                [1, 500],
                [0, 501]
            );
            Field(
                data,
                "action.47.E11",
                initiate,
                "wf_act_init_transaction",
                initiateFresh,
                2,
                [0, 1],
                [2]
            );
            Field(
                data,
                "action.47.E12",
                initiate,
                "wf_act_init_transaction",
                initiateFresh,
                3,
                Targets,
                [-1, 2]
            );
            Field(
                data,
                "action.47.E13",
                initiate,
                "wf_act_init_transaction",
                initiateFresh,
                4,
                [0, 1],
                [2]
            );
            // NumberInputParam(300, 30, 3600).
            Field(
                data,
                "action.47.E14",
                initiate,
                "wf_act_init_transaction",
                initiateFresh,
                5,
                [30, 3600],
                [29, 3601]
            );

            // Cancel Transaction: match radio 0 the picked contract, 1 any (chests/_-F2F.as:32).
            Field(
                data,
                "action.48.E9",
                typeof(WiredActionCancelTransaction),
                "wf_act_cancel_transaction",
                [0],
                0,
                [0, 1],
                [2]
            );

            // Chest Furni of Type scanner: scanning radio 0 all items, 1 previewed only
            // (addons/chests/_-N1u.as:74).
            Field(
                data,
                "addon.18.E9",
                typeof(WiredAddonChestItemTypeScanner),
                "wf_xtra_scan_chest_furni_by_type",
                [0],
                0,
                [0, 1],
                [2]
            );

            // Custom Contract (addons/chests/_-P2o.as:53, 115-125): per side [on, coins 0 / furni
            // 1, amount is variable, amount 1..100000, amount target], payment then reward.
            var contract = typeof(WiredAddonCustomContract);
            int[] contractFresh = [0, 0, 0, 1, -20, 0, 0, 0, 1, -20];

            foreach (var (side, first) in new[] { (0, 9), (5, 15) })
            {
                Field(
                    data,
                    $"addon.20.E{first}",
                    contract,
                    "wf_xtra_custom_contract",
                    contractFresh,
                    side,
                    [0, 1],
                    [2]
                );
                Field(
                    data,
                    $"addon.20.E{first + 1}",
                    contract,
                    "wf_xtra_custom_contract",
                    contractFresh,
                    side + 1,
                    [0, 1],
                    [2]
                );
                Field(
                    data,
                    $"addon.20.E{first + 2}",
                    contract,
                    "wf_xtra_custom_contract",
                    contractFresh,
                    side + 2,
                    [0, 1],
                    [2]
                );
                Field(
                    data,
                    $"addon.20.E{first + 3}",
                    contract,
                    "wf_xtra_custom_contract",
                    contractFresh,
                    side + 3,
                    [1, 100000],
                    [0, 100001]
                );
                Field(
                    data,
                    $"addon.20.E{first + 4}",
                    contract,
                    "wf_xtra_custom_contract",
                    contractFresh,
                    side + 4,
                    Targets,
                    [-1, 2]
                );
            }

            // Chest Has Amount / Chest Has Item Types (conditions/chests/ChestHasAmount.as:37,77-79):
            // [amount 0..1000000, amount is variable, amount target, comparison radio].
            foreach (
                var (code, logic, name) in new (string, Type, string)[]
                {
                    ("condition.45", typeof(WiredConditionChestHasItems), "wf_cnd_chest_has_items"),
                    (
                        "condition.46",
                        typeof(WiredConditionChestHasItemTypes),
                        "wf_cnd_chest_has_item_type"
                    ),
                }
            )
            {
                int[] fresh = [0, 0, -20, 2];

                Field(data, $"{code}.E9", logic, name, fresh, 0, [0, 1000000], [-1, 1000001]);
                Field(data, $"{code}.E10", logic, name, fresh, 1, [0, 1], [2]);
                Field(data, $"{code}.E11", logic, name, fresh, 2, Targets, [-1, 2]);
                // The radio offers > ≥ = ≤ < ≠ as 2, 5, 1, 3, 0, 4.
                Field(data, $"{code}.E12", logic, name, fresh, 3, [2, 5, 1, 3, 0, 4], [-1, 6]);
            }

            // Bot Talk / Bot Talk To Avatar (userdefinedroomevents/actiontypes/_-O15.as and
            // _-A1j.as:72,86-91): [talk 0 / shout or whisper 1, bubble width -1..2, hide name].
            foreach (
                var (code, logic, name) in new (string, Type, string)[]
                {
                    ("action.23", typeof(WiredActionBotTalk), "wf_act_bot_talk"),
                    ("action.27", typeof(WiredActionBotTalkToAvatar), "wf_act_bot_talk_to_avatar"),
                }
            )
            {
                int[] fresh = [0, -1, 0];

                Field(data, $"{code}.E11", logic, name, fresh, 0, [0, 1], [2]);
                Field(data, $"{code}.E12", logic, name, fresh, 1, [-1, 0, 1, 2], [-2, 3]);
                // The hide bot name checkbox (not a matrix field, so no case id of its own).
                Field(data, $"{code}.hide_bot_name", logic, name, fresh, 2, [0, 1], [2]);
            }

            // Bot Give Hand Item: the dropdown's fixed codes, then any captured hand item; "none"
            // (-1) is written as 0 (actiontypes/_-t1P.as:22,51-52,107-126).
            Field(
                data,
                "action.24.E10",
                typeof(WiredActionBotGiveHandItem),
                "wf_act_bot_give_handitem",
                [2],
                0,
                [0, 2, 5, 7, 8, 9, 10, 27, 1126, 1127, 1128, 3000],
                [-1]
            );

            // Bot Follow Avatar: radio 1 start, 0 stop (actiontypes/_-4X.as:56).
            Field(
                data,
                "action.25.E10",
                typeof(WiredActionBotFollowAvatar),
                "wf_act_bot_follow_avatar",
                [1],
                0,
                [0, 1],
                [2]
            );

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Fields))]
    public async Task An_official_field_value_is_saved_and_one_the_editor_lacks_is_refused(
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
            box.GetSnapshot().IntParams.Should().Equal(ints, caseId);
    }

    /// <summary>
    /// The bot name input of every bot box takes 32 characters
    /// (<c>TextInputParam("", 32, ...)</c>: actiontypes/_-E1d.as:44, _-h2d.as:44, _-O15.as:82,
    /// _-t1P.as:78, _-4X.as:55, _-U7.as:65, _-A1j.as:82; triggerconfs/_-lE.as:39, _-XB.as:39).
    /// The talk and dress boxes write the name, a tab, then the message or figure.
    /// </summary>
    [Theory]
    [InlineData("action.21.E9", typeof(WiredActionBotTeleport), "wf_act_bot_teleport", new int[0])]
    [InlineData("action.22.E9", typeof(WiredActionBotMove), "wf_act_bot_move", new int[0])]
    [InlineData("action.23.E9", typeof(WiredActionBotTalk), "wf_act_bot_talk", new[] { 0, -1, 0 })]
    [InlineData(
        "action.24.E9",
        typeof(WiredActionBotGiveHandItem),
        "wf_act_bot_give_handitem",
        new[] { 2 }
    )]
    [InlineData(
        "action.25.E9",
        typeof(WiredActionBotFollowAvatar),
        "wf_act_bot_follow_avatar",
        new[] { 1 }
    )]
    [InlineData(
        "action.26.E9",
        typeof(WiredActionBotChangeFigure),
        "wf_act_bot_clothes",
        new int[0]
    )]
    [InlineData(
        "action.27.E9",
        typeof(WiredActionBotTalkToAvatar),
        "wf_act_bot_talk_to_avatar",
        new[] { 0, -1, 0 }
    )]
    [InlineData(
        "trigger.13.E9",
        typeof(WiredTriggerBotReachesItem),
        "wf_trg_bot_reached_stf",
        new int[0]
    )]
    [InlineData(
        "trigger.14.E9",
        typeof(WiredTriggerBotReachesHabbo),
        "wf_trg_bot_reached_avtr",
        new int[0]
    )]
    public async Task A_bot_box_keeps_a_32_character_bot_name(
        string caseId,
        Type logic,
        string name,
        int[] ints
    )
    {
        var box = AddBox(logic, name);
        var text = new string('b', 32);

        (await SaveAsync(box, ints, text)).IsSaved.Should().BeTrue(caseId);
        box.GetSnapshot().StringParam.Should().Be(text, caseId);
    }

    /// <summary>
    /// action.23.E10 / action.27.E10: the bot message editor takes 400 characters on 8 lines
    /// (<c>TextAreaParam(40, -1, 8, -1, 400, ...)</c>, actiontypes/_-O15.as:84 and _-A1j.as:84;
    /// 400 in the newest editor), written after the bot name and a tab.
    /// </summary>
    [Theory]
    [InlineData("action.23.E10", typeof(WiredActionBotTalk), "wf_act_bot_talk")]
    [InlineData("action.27.E10", typeof(WiredActionBotTalkToAvatar), "wf_act_bot_talk_to_avatar")]
    public async Task A_bot_talk_box_keeps_a_400_character_message_on_8_lines(
        string caseId,
        Type logic,
        string name
    )
    {
        var box = AddBox(logic, name);
        var lines = Enumerable.Range(0, 8).Select(_ => new string('m', 49));
        var message = string.Join('\n', lines) + "m";
        var text = new string('b', 32) + "\t" + message;

        message.Length.Should().Be(400);
        (await SaveAsync(box, [0, -1, 0], text)).IsSaved.Should().BeTrue(caseId);
        box.GetSnapshot().StringParam.Should().Be(text, caseId);
    }

    /// <summary>
    /// action.24.E9: an empty name is the hand item editor's "no bot" (its bot.usage checkbox
    /// unticked writes "", actiontypes/_-t1P.as:44-47), and saves.
    /// </summary>
    [Fact]
    public async Task Bot_give_hand_item_saves_with_no_bot_named_action_24_E9()
    {
        var box = AddBox(typeof(WiredActionBotGiveHandItem), "wf_act_bot_give_handitem");

        (await SaveAsync(box, [2], "")).IsSaved.Should().BeTrue();
        box.GetSnapshot().StringParam.Should().BeEmpty();
    }

    /// <summary>
    /// action.26.E10: the dress box writes the name, a tab and the editor's own figure string,
    /// captured as it is (actiontypes/_-U7.as:39,75); the whole figure is kept.
    /// </summary>
    [Fact]
    public async Task Bot_change_figure_keeps_the_captured_figure_action_26_E10()
    {
        var box = AddBox(typeof(WiredActionBotChangeFigure), "wf_act_bot_clothes");
        const string figure =
            "hd-180-1.ch-3030-1408.lg-275-110.sh-905-110.hr-828-61.ha-3786-92.ea-1406-0.fa-1201-0.cc-3039-110.ca-1813-0.wa-2001-0.cp-3204-110";
        var text = new string('b', 32) + "\t" + figure;

        (await SaveAsync(box, [], text)).IsSaved.Should().BeTrue();
        box.GetSnapshot().StringParam.Should().Be(text);
    }

    /// <summary>
    /// action.45.E15 / action.46.E15: the reward pop-up text takes 200 characters on 3 lines
    /// (<c>TextAreaParam(45, -1, 3, -1, 200, ...)</c>, chests/_-U1N.as:150); a longer text did
    /// not come from the editor and is cut to 200.
    /// </summary>
    [Theory]
    [InlineData(
        "action.45.E15",
        typeof(WiredActionGiveCurrencyFromChest),
        "wf_act_give_currency",
        11
    )]
    [InlineData("action.46.E15", typeof(WiredActionGiveFurniFromChest), "wf_act_give_furni", 0)]
    public async Task A_give_from_chest_box_keeps_a_200_character_popup_text_on_3_lines(
        string caseId,
        Type logic,
        string name,
        int kind
    )
    {
        var box = AddBox(logic, name);
        var text = new string('p', 66) + "\n" + new string('p', 66) + "\n" + new string('p', 66);

        text.Length.Should().Be(200);
        (await SaveAsync(box, [0, 1, 0, -20, 1, kind], text)).IsSaved.Should().BeTrue(caseId);
        box.GetSnapshot().StringParam.Should().Be(text, caseId);

        (await SaveAsync(box, [0, 1, 0, -20, 1, kind], text + "p")).IsSaved.Should().BeTrue(caseId);
        box.GetSnapshot().StringParam.Should().Be(text, caseId);
    }

    /// <summary>
    /// The value-or-variable boxes write their variable ids positionally, "n" for none
    /// (<c>finalizeSelection</c>: chests/_-U1N.as:59, _-D13.as:50, addons/chests/_-N1u.as:69,
    /// _-P2o.as:58, conditions/chests/ChestHasAmount.as:42). A box saved in constant mode keeps
    /// one empty slot per variable, so the reward side's variable never slides into the
    /// payment's.
    /// </summary>
    [Theory]
    [InlineData(
        "action.45.E14",
        typeof(WiredActionGiveCurrencyFromChest),
        "wf_act_give_currency",
        new[] { 0, 1, 0, -20, 1, 11 },
        1
    )]
    [InlineData(
        "action.46.E14",
        typeof(WiredActionGiveFurniFromChest),
        "wf_act_give_furni",
        new[] { 0, 1, 0, -20, 1, 0 },
        1
    )]
    [InlineData(
        "action.47.E15",
        typeof(WiredActionInitiateTransaction),
        "wf_act_init_transaction",
        new[] { 0, 10, 0, -20, 0, 300 },
        1
    )]
    [InlineData(
        "addon.20.E14",
        typeof(WiredAddonCustomContract),
        "wf_xtra_custom_contract",
        new[] { 0, 0, 0, 1, -20, 0, 0, 0, 1, -20 },
        2
    )]
    [InlineData(
        "condition.45.E13",
        typeof(WiredConditionChestHasItems),
        "wf_cnd_chest_has_items",
        new[] { 0, 0, -20, 2 },
        1
    )]
    [InlineData(
        "condition.46.E13",
        typeof(WiredConditionChestHasItemTypes),
        "wf_cnd_chest_has_item_type",
        new[] { 0, 0, -20, 2 },
        1
    )]
    public async Task A_box_saved_without_a_variable_keeps_an_empty_slot_per_variable(
        string caseId,
        Type logic,
        string name,
        int[] ints,
        int slots
    )
    {
        var box = AddBox(logic, name);

        (await SaveAsync(box, ints, variableIds: [.. Enumerable.Repeat("n", slots)]))
            .IsSaved.Should()
            .BeTrue(caseId);
        box.GetSnapshot()
            .VariableIds.Should()
            .Equal(Enumerable.Repeat(new WiredVariableId(0), slots), caseId);
    }

    /// <summary>Adds the rows of one int field: each accepted value saved, each refused one not.</summary>
    private static void Field(
        TheoryData<string, Type, string, int[], bool> data,
        string caseId,
        Type logic,
        string name,
        int[] fresh,
        int index,
        int[] accepted,
        int[] refused
    )
    {
        foreach (var (values, saved) in new[] { (accepted, true), (refused, false) })
        {
            foreach (var value in values)
            {
                var ints = (int[])fresh.Clone();

                ints[index] = value;
                data.Add(caseId, logic, name, ints, saved);
            }
        }
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
        string text = "",
        string[]? variableIds = null
    ) =>
        box switch
        {
            FurnitureWiredConditionLogic => _room.SaveWithResultAsync<UpdateConditionMessage>(
                BOX,
                intParams: ints,
                stringParam: text,
                variableIds: variableIds
            ),
            FurnitureWiredTriggerLogic => _room.SaveWithResultAsync<UpdateTriggerMessage>(
                BOX,
                intParams: ints,
                stringParam: text,
                variableIds: variableIds
            ),
            FurnitureWiredAddonLogic => _room.SaveWithResultAsync<UpdateAddonMessage>(
                BOX,
                intParams: ints,
                stringParam: text,
                variableIds: variableIds
            ),
            _ => _room.SaveWithResultAsync<UpdateActionMessage>(
                BOX,
                intParams: ints,
                stringParam: text,
                definitionSpecifics: [0],
                variableIds: variableIds
            ),
        };
}
