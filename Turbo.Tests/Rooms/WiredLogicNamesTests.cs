using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Every wired box of the hotel's furnidata has a logic of its own name: a furni definition's
/// logic is its classname, so a box whose logic is registered under another name stays plain
/// furniture. The Variable FX add-ons were registered as <c>wf_xtra_var_fx_*</c> while the boxes
/// are <c>wf_xtra_varfx_*</c>. (<c>wf_xtra_var_web_api</c>, the Variables web API, is not built.)
/// </summary>
public sealed class WiredLogicNamesTests
{
    private static readonly string[] HOTEL_WIRED_BOXES =
    [
        "wf_act_adjust_clock",
        "wf_act_bot_clothes",
        "wf_act_bot_follow_avatar",
        "wf_act_bot_give_handitem",
        "wf_act_bot_move",
        "wf_act_bot_talk",
        "wf_act_bot_talk_to_avatar",
        "wf_act_bot_teleport",
        "wf_act_call_stacks",
        "wf_act_cancel_transaction",
        "wf_act_change_var_val",
        "wf_act_chase",
        "wf_act_click_conf",
        "wf_act_control_clock",
        "wf_act_flee",
        "wf_act_freeze",
        "wf_act_furni_to_furni",
        "wf_act_furni_to_user",
        "wf_act_give_currency",
        "wf_act_give_furni",
        "wf_act_give_reward",
        "wf_act_give_score",
        "wf_act_give_score_tm",
        "wf_act_give_var",
        "wf_act_init_transaction",
        "wf_act_join_team",
        "wf_act_kick_user",
        "wf_act_leave_team",
        "wf_act_log",
        "wf_act_match_to_sshot",
        "wf_act_move_furni_as_group",
        "wf_act_move_furni_to",
        "wf_act_move_rotate",
        "wf_act_move_rotate_user",
        "wf_act_move_to_dir",
        "wf_act_mute_triggerer",
        "wf_act_neg_call_stacks",
        "wf_act_neg_log",
        "wf_act_neg_send_signal",
        "wf_act_place_furni",
        "wf_act_progress_ach",
        "wf_act_rel_mov",
        "wf_act_remove_furni",
        "wf_act_remove_var",
        "wf_act_reset_timers",
        "wf_act_send_signal",
        "wf_act_set_altitude",
        "wf_act_show_message",
        "wf_act_teleport_to",
        "wf_act_teleport_to_room",
        "wf_act_toggle_state",
        "wf_act_toggle_to_rnd",
        "wf_act_unfreeze",
        "wf_act_user_to_furni",
        "wf_cnd_actor_dir",
        "wf_cnd_actor_in_group",
        "wf_cnd_actor_in_team",
        "wf_cnd_chest_has_item_type",
        "wf_cnd_chest_has_items",
        "wf_cnd_counter_time_matches",
        "wf_cnd_date_rng_active",
        "wf_cnd_furnis_hv_avtrs",
        "wf_cnd_has_altitude",
        "wf_cnd_has_furni_on",
        "wf_cnd_has_handitem",
        "wf_cnd_has_var",
        "wf_cnd_match_date",
        "wf_cnd_match_snapshot",
        "wf_cnd_match_time",
        "wf_cnd_neg_has_var",
        "wf_cnd_not_furni_on",
        "wf_cnd_not_has_handitem",
        "wf_cnd_not_hv_avtrs",
        "wf_cnd_not_in_group",
        "wf_cnd_not_in_team",
        "wf_cnd_not_match_snap",
        "wf_cnd_not_stuff_is",
        "wf_cnd_not_trggrer_on",
        "wf_cnd_not_triggerer_match",
        "wf_cnd_not_user_count",
        "wf_cnd_not_user_performs_action",
        "wf_cnd_not_wearing_b",
        "wf_cnd_not_wearing_fx",
        "wf_cnd_slc_quantity",
        "wf_cnd_stuff_is",
        "wf_cnd_team_has_rank",
        "wf_cnd_team_has_score",
        "wf_cnd_time_less_than",
        "wf_cnd_time_more_than",
        "wf_cnd_trggrer_on_frn",
        "wf_cnd_triggerer_match",
        "wf_cnd_user_count_in",
        "wf_cnd_user_performs_action",
        "wf_cnd_valid_moves",
        "wf_cnd_var_age_match",
        "wf_cnd_var_val_match",
        "wf_cnd_wearing_badge",
        "wf_cnd_wearing_effect",
        "wf_slc_furni_altitude",
        "wf_slc_furni_area",
        "wf_slc_furni_bytype",
        "wf_slc_furni_neighborhood",
        "wf_slc_furni_onfurni",
        "wf_slc_furni_picks",
        "wf_slc_furni_signal",
        "wf_slc_furni_with_var",
        "wf_slc_remote",
        "wf_slc_users_area",
        "wf_slc_users_byaction",
        "wf_slc_users_byname",
        "wf_slc_users_bytype",
        "wf_slc_users_group",
        "wf_slc_users_handitem",
        "wf_slc_users_neighborhood",
        "wf_slc_users_onfurni",
        "wf_slc_users_signal",
        "wf_slc_users_team",
        "wf_slc_users_with_var",
        "wf_trg_at_given_time",
        "wf_trg_at_time_long",
        "wf_trg_bot_reached_avtr",
        "wf_trg_bot_reached_stf",
        "wf_trg_click_furni",
        "wf_trg_click_tile",
        "wf_trg_click_user",
        "wf_trg_clock_counter",
        "wf_trg_collision",
        "wf_trg_enter_room",
        "wf_trg_game_ends",
        "wf_trg_game_starts",
        "wf_trg_leave_room",
        "wf_trg_period_long",
        "wf_trg_period_short",
        "wf_trg_periodically",
        "wf_trg_recv_signal",
        "wf_trg_says_something",
        "wf_trg_score_achieved",
        "wf_trg_state_changed",
        "wf_trg_stuff_state",
        "wf_trg_transaction_complete",
        "wf_trg_transaction_fail",
        "wf_trg_user_performs_action",
        "wf_trg_var_changed",
        "wf_trg_walks_off_furni",
        "wf_trg_walks_on_furni",
        "wf_var_context",
        "wf_var_echo",
        "wf_var_furni",
        "wf_var_quest",
        "wf_var_quest_chain",
        "wf_var_reference",
        "wf_var_room",
        "wf_var_user",
        "wf_xtra_achievement_enabler",
        "wf_xtra_anim_time",
        "wf_xtra_custom_contract",
        "wf_xtra_exec_in_order",
        "wf_xtra_execution_limit",
        "wf_xtra_filter_furni",
        "wf_xtra_filter_furni_by_var",
        "wf_xtra_filter_users",
        "wf_xtra_filter_users_by_var",
        "wf_xtra_mov_carry_users",
        "wf_xtra_mov_curve",
        "wf_xtra_mov_no_animation",
        "wf_xtra_mov_physics",
        "wf_xtra_or_eval",
        "wf_xtra_random",
        "wf_xtra_rotate_to_dir",
        "wf_xtra_scan_chest_furni_by_type",
        "wf_xtra_text_input_variable",
        "wf_xtra_text_output_furni_name",
        "wf_xtra_text_output_username",
        "wf_xtra_text_output_variable",
        "wf_xtra_unseen",
        "wf_xtra_var_lvlup_system",
        "wf_xtra_var_text_connector",
        "wf_xtra_var_time_util",
        "wf_xtra_varfx_boss",
        "wf_xtra_varfx_hp",
        "wf_xtra_varfx_levelling",
        "wf_xtra_varfx_number",
        "wf_xtra_varfx_prog",
        "wf_xtra_varfx_status",
    ];

    [Fact]
    public void Every_wired_box_of_the_hotel_has_a_logic_of_its_name()
    {
        var registered = typeof(FurnitureWiredLogic)
            .Assembly.GetTypes()
            .Select(type => type.GetCustomAttribute<RoomObjectLogicAttribute>(false)?.Key)
            .Where(key => key is not null)
            .ToHashSet();

        HOTEL_WIRED_BOXES.Where(name => !registered.Contains(name)).Should().BeEmpty();
    }

    /// <summary>
    /// On the test hotel the definitions of eight boxes (Variable Changed, both text output
    /// placeholders, Give / Remove / Change Variable, Unfreeze, Actor Direction) name no wired
    /// logic, so double-clicking them opened nothing. A wired classname with a logic of its own
    /// name gets that logic, whatever the definition's logic column says.
    /// </summary>
    [Theory]
    [InlineData("default_floor")]
    [InlineData("not_a_logic")]
    public void A_wired_box_gets_its_classnames_logic_whatever_its_definition_names(
        string definitionLogic
    )
    {
        var room = new WiredRoom();
        var item = room.AddFloorItem(40, 1, 1, "wf_trg_var_changed");

        item.GetType()
            .GetProperty(nameof(item.Definition))!
            .SetValue(item, item.Definition with { LogicName = definitionLogic });

        room.Harness.LogicProvider.CreateLogicInstance(
                definitionLogic,
                new RoomFloorItemContext(room.Harness.Room, item)
            )
            .Should()
            .BeOfType<WiredTriggerVariableChanged>();
    }
}
