using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The int fields of the user boxes (wired matrix family W3, case ids <c>&lt;holder&gt;.&lt;code&gt;.E&lt;n&gt;</c>):
/// every value the official editor can send is saved as it is, and one the editor does not offer
/// is refused. The editors' widgets are cited per case (AS3 WIN63-202610201032, paths under
/// roomevents/); how the official server answers a value no editor sends is not known, so the
/// refusals are ours.
/// </summary>
public sealed class WiredUserBoxFieldsTests
{
    private const int BOX = 1;

    private readonly WiredRoom _room = new(8, 8);

    public static TheoryData<string, Type, string, int[], bool> Fields =>
        new()
        {
            // action.17.E9: interval radio 0 once, 1 days, 2 hours, 3 minutes (GiveReward.as:133).
            {
                "action.17.E9",
                typeof(WiredActionGiveReward),
                "wf_act_give_reward",
                [3, 0, 0, 1],
                true
            },
            {
                "action.17.E9",
                typeof(WiredActionGiveReward),
                "wf_act_give_reward",
                [4, 0, 0, 1],
                false
            },
            // action.17.E10: unique prize checkbox.
            {
                "action.17.E10",
                typeof(WiredActionGiveReward),
                "wf_act_give_reward",
                [0, 1, 0, 1],
                true
            },
            // action.17.E11: 0 without the limit, else NumberInputParam(1, 1, 1000) (GiveReward.as:123).
            {
                "action.17.E11",
                typeof(WiredActionGiveReward),
                "wf_act_give_reward",
                [0, 0, 1000, 1],
                true
            },
            {
                "action.17.E11",
                typeof(WiredActionGiveReward),
                "wf_act_give_reward",
                [0, 0, 1001, 1],
                false
            },
            // action.17.E12: "n =" takes four digits, written as at least 1 (GiveReward.as:99,133).
            {
                "action.17.E12",
                typeof(WiredActionGiveReward),
                "wf_act_give_reward",
                [3, 0, 0, 9999],
                true
            },
            {
                "action.17.E12",
                typeof(WiredActionGiveReward),
                "wf_act_give_reward",
                [2, 0, 0, 1001],
                true
            },
            {
                "action.17.E12",
                typeof(WiredActionGiveReward),
                "wf_act_give_reward",
                [3, 0, 0, 0],
                false
            },
            // action.20: minutes slider 0..10 (MuteUser.as:59).
            {
                "action.20.E10",
                typeof(WiredActionMuteTriggerer),
                "wf_act_mute_triggerer",
                [0],
                true
            },
            {
                "action.20.E10",
                typeof(WiredActionMuteTriggerer),
                "wf_act_mute_triggerer",
                [10],
                true
            },
            {
                "action.20.E10",
                typeof(WiredActionMuteTriggerer),
                "wf_act_mute_triggerer",
                [11],
                false
            },
            // action.31.E10: unfreeze when teleporting (actiontypes/_-61o.as:49).
            { "action.31.E10", typeof(WiredActionFreezeUser), "wf_act_freeze", [4, 1], true },
            // action.42.E9: movement -1 or a direction 0..7 (actiontypes/_-j2J.as:49-57).
            {
                "action.42.E9",
                typeof(WiredActionMoveRotateUser),
                "wf_act_move_rotate_user",
                [7, -1],
                true
            },
            {
                "action.42.E9",
                typeof(WiredActionMoveRotateUser),
                "wf_act_move_rotate_user",
                [8, -1],
                false
            },
            // action.42.E10: rotation -1, 0..7, 9 clockwise, 10 counter-clockwise; no 8.
            {
                "action.42.E10",
                typeof(WiredActionMoveRotateUser),
                "wf_act_move_rotate_user",
                [-1, 7],
                true
            },
            {
                "action.42.E10",
                typeof(WiredActionMoveRotateUser),
                "wf_act_move_rotate_user",
                [-1, 9],
                true
            },
            {
                "action.42.E10",
                typeof(WiredActionMoveRotateUser),
                "wf_act_move_rotate_user",
                [-1, 10],
                true
            },
            {
                "action.42.E10",
                typeof(WiredActionMoveRotateUser),
                "wf_act_move_rotate_user",
                [-1, 8],
                false
            },
            // action.43.E9: walk mode radio 0, 1, 2 (actiontypes/_-u10.as:59).
            { "action.43.E9", typeof(WiredActionUserToFurni), "wf_act_user_to_furni", [2], true },
            { "action.43.E9", typeof(WiredActionUserToFurni), "wf_act_user_to_furni", [3], false },
            // action.52.E9-E11: NumberInputParam(13, 0, 10000), NumberInputParam(2, 0, 2), type
            // radio 0/1 (actiontypes/_-K1A.as:40-44).
            {
                "action.52.E9",
                typeof(WiredActionGiveEffect),
                "wf_act_give_effect",
                [10000, 2, 0],
                true
            },
            {
                "action.52.E9",
                typeof(WiredActionGiveEffect),
                "wf_act_give_effect",
                [10001, 2, 0],
                false
            },
            {
                "action.52.E10",
                typeof(WiredActionGiveEffect),
                "wf_act_give_effect",
                [13, 0, 0],
                true
            },
            {
                "action.52.E10",
                typeof(WiredActionGiveEffect),
                "wf_act_give_effect",
                [13, 3, 0],
                false
            },
            {
                "action.52.E11",
                typeof(WiredActionGiveEffect),
                "wf_act_give_effect",
                [13, 2, 1],
                true
            },
            // action.7.E10: visibility radio 0 the user, 1 everyone (actiontypes/_-21t.as:76).
            {
                "action.7.E10",
                typeof(WiredActionShowMessage),
                "wf_act_show_message",
                [1, 202, -1],
                true
            },
            {
                "action.7.E10",
                typeof(WiredActionShowMessage),
                "wf_act_show_message",
                [2, 202, -1],
                false
            },
            // action.7.E11: the style dropdown's NOTIFICATION_STYLES only (actiontypes/_-21t.as:22).
            {
                "action.7.E11",
                typeof(WiredActionShowMessage),
                "wf_act_show_message",
                [0, 34, -1],
                true
            },
            {
                "action.7.E11",
                typeof(WiredActionShowMessage),
                "wf_act_show_message",
                [0, 252, -1],
                true
            },
            {
                "action.7.E11",
                typeof(WiredActionShowMessage),
                "wf_act_show_message",
                [0, 5, -1],
                false
            },
            {
                "action.7.E11",
                typeof(WiredActionShowMessage),
                "wf_act_show_message",
                [0, 203, -1],
                false
            },
            // action.7.E12: bubble width -1, 0, 1, 2 (actiontypes/_-21t.as:83).
            {
                "action.7.E12",
                typeof(WiredActionShowMessage),
                "wf_act_show_message",
                [0, 202, 2],
                true
            },
            {
                "action.7.E12",
                typeof(WiredActionShowMessage),
                "wf_act_show_message",
                [0, 202, 3],
                false
            },
            // action.8.E9: fast teleport checkbox (actiontypes/_-U1S.as:48).
            { "action.8.E9", typeof(WiredActionTeleportTo), "wf_act_teleport_to", [1], true },
            // condition.12.E9: NumberInputParam(0, -2147483648, 2147483647) (conditions/_-w1h.as:38).
            {
                "condition.12.E9",
                typeof(WiredConditionHabboHasEffect),
                "wf_cnd_wearing_effect",
                [20000],
                true
            },
            {
                "condition.12.E9",
                typeof(WiredConditionHabboHasEffect),
                "wf_cnd_wearing_effect",
                [-1],
                true
            },
            {
                "condition.12.E9",
                typeof(WiredConditionHabboHasEffect),
                "wf_cnd_wearing_effect",
                [int.MaxValue],
                true
            },
            // condition.26.E9: user type radio 1 Habbo, 2 pet, 4 bot (conditions/TriggererMatches.as:37).
            {
                "condition.26.E9",
                typeof(WiredConditionExecutorMatches),
                "wf_cnd_triggerer_match",
                [4],
                true
            },
            // condition.32.E9 / trigger.16.E9: WiredUserAction codes 0..8, 10, 11, 67; no 9.
            {
                "condition.32.E9",
                typeof(WiredConditionHabboPerformsAction),
                "wf_cnd_user_performs_action",
                [67],
                true
            },
            {
                "condition.32.E9",
                typeof(WiredConditionHabboPerformsAction),
                "wf_cnd_user_performs_action",
                [9],
                false
            },
            {
                "trigger.16.E9",
                typeof(WiredTriggerHabboPerformsAction),
                "wf_trg_user_performs_action",
                [11],
                true
            },
            {
                "trigger.16.E9",
                typeof(WiredTriggerHabboPerformsAction),
                "wf_trg_user_performs_action",
                [9],
                false
            },
            // condition.37.E9: one bit per direction icon, bits 0..7 (conditions/_-ay.as:74).
            {
                "condition.37.E9",
                typeof(WiredConditionEntityDirection),
                "wf_cnd_actor_dir",
                [255],
                true
            },
            // condition.5: two sliders 0..125 (conditions/UserCountIn.as:34-35).
            {
                "condition.5.E9",
                typeof(WiredConditionRoomHabboCount),
                "wf_cnd_user_count_in",
                [0, 125],
                true
            },
            {
                "condition.5.E9",
                typeof(WiredConditionRoomHabboCount),
                "wf_cnd_user_count_in",
                [0, 126],
                false
            },
            // trigger.0.E9-E11: owner only, match mode 0..2, hide (triggerconfs/_-i2o.as:43-50).
            {
                "trigger.0.E10",
                typeof(WiredTriggerHabboSaysKeyword),
                "wf_trg_says_something",
                [1, 2, 0],
                true
            },
            {
                "trigger.0.E10",
                typeof(WiredTriggerHabboSaysKeyword),
                "wf_trg_says_something",
                [0, 3, 1],
                false
            },
            // trigger.24.E9-E10: two checkboxes (triggerconfs/_-L20.as:34).
            { "trigger.24.E9", typeof(WiredTriggerClickUser), "wf_trg_click_user", [0, 1], true },
            // addon.2000.E9-E11: mode 0 fixed / 1 another room, always 0, the room id in mode 1
            // (wired_setup/addons/GlobalPlaceholderAddon.as:149).
            {
                "addon.2000.E9",
                typeof(WiredAddonGlobalPlaceholder),
                "wf_xtra_global_placeholder",
                [1, 0, 12],
                true
            },
            {
                "addon.2000.E10",
                typeof(WiredAddonGlobalPlaceholder),
                "wf_xtra_global_placeholder",
                [0, 1, 0],
                false
            },
        };

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
    /// action.7.E9: the message editor takes 400 characters since WIN63-202610121624
    /// (<c>TextAreaParam(40, -1, 8, -1, 400)</c>, actiontypes/_-21t.as:74).
    /// </summary>
    [Fact]
    public async Task Show_message_keeps_a_400_character_message_action_7_E9()
    {
        var box = AddBox(typeof(WiredActionShowMessage), "wf_act_show_message");
        var text = new string('a', 400);

        (await SaveAsync(box, [0, 202, -1], text)).IsSaved.Should().BeTrue();
        box.GetSnapshot().StringParam.Should().Be(text);
    }

    /// <summary>
    /// action.19.E9 / action.20.E9: Kick and Mute take a 100 character message
    /// (<c>TextInputParam("", 100)</c>, KickFromRoom.as:49, MuteUser.as:58).
    /// </summary>
    [Theory]
    [InlineData("action.19.E9", typeof(WiredActionKickUser), "wf_act_kick_user", new int[0])]
    [InlineData(
        "action.20.E9",
        typeof(WiredActionMuteTriggerer),
        "wf_act_mute_triggerer",
        new[] { 1 }
    )]
    public async Task Kick_and_mute_keep_a_100_character_message(
        string caseId,
        Type logic,
        string name,
        int[] ints
    )
    {
        var box = AddBox(logic, name);
        var text = new string('k', 100);

        (await SaveAsync(box, ints, text)).IsSaved.Should().BeTrue(caseId);
        box.GetSnapshot().StringParam.Should().Be(text, caseId);
    }

    /// <summary>
    /// trigger.0.E12: the keyword input takes 1000 characters
    /// (<c>TextInputParam("", 1000, ...)</c>, triggerconfs/_-i2o.as:42); all of it is kept.
    /// </summary>
    [Fact]
    public async Task Says_something_keeps_a_1000_character_keyword_trigger_0_E12()
    {
        var box = AddBox(typeof(WiredTriggerHabboSaysKeyword), "wf_trg_says_something");
        var text = new string('w', 1000);

        (await SaveAsync(box, [0, 0, 1], text)).IsSaved.Should().BeTrue();
        box.GetSnapshot().StringParam.Should().Be(text);
    }

    /// <summary>
    /// condition.32.E10 / trigger.16.E10: the extra filter is the sign id in decimal (0..17) or
    /// "dance N" (N 1..4), empty for no filter (WiredUserAction.as:6-18).
    /// </summary>
    [Theory]
    [InlineData(WiredAvatarActionType.Sign, "17", 17)]
    [InlineData(WiredAvatarActionType.Sign, "0", 0)]
    [InlineData(WiredAvatarActionType.Dance, "dance 4", 4)]
    [InlineData(WiredAvatarActionType.Dance, "dance 1", 1)]
    [InlineData(WiredAvatarActionType.Sign, "", null)]
    [InlineData(WiredAvatarActionType.Dance, "", null)]
    public void The_action_filter_reads_the_editors_sign_and_dance_text_condition_32_E10(
        WiredAvatarActionType action,
        string filter,
        int? expected
    ) => WiredAvatarActionMatcher.ParseExtra(action, filter).Should().Be(expected);

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
