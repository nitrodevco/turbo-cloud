using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Every wired box opens, before it was ever saved, with the int params and the default furni
/// and user sources the official server sends for a freshly placed box of the same kind
/// (official client capture: one editor open per box, WIN63-202610201032 and earlier; the
/// recording's timestamp is the second column). The int counts, the defaults and the number of
/// source slots all come from that reply. Not here: wf_trg_at_time_long (no recording of its
/// own; the hotel files it under the code of At Given Time) and the Conditions Evaluation
/// add-on, whose furni slot ([[0,100,200,201]], default [100]) has no use the editor shows
/// (AS3 wired_setup/addons/_-Tz.as), so only its ints are held to the official ones below.
/// </summary>
public sealed class WiredOfficialFreshDefaultsTests
{
    private const int BOX = 2;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, string, int[], int[], int[]> OfficialFreshBoxes =>
        new()
        {
            { "wf_act_adjust_clock", "2026-10-20T17-39-35", [0, 0, 0, 0], [100], [] },
            { "wf_act_bot_clothes", "2026-10-20T17-39-35", [], [], [100] },
            { "wf_act_bot_follow_avatar", "2026-10-20T17-39-35", [1], [], [100, 0] },
            { "wf_act_bot_give_handitem", "2026-10-20T17-39-35", [2], [], [100, 0] },
            { "wf_act_bot_move", "2026-10-20T17-39-35", [], [100], [100] },
            { "wf_act_bot_talk", "2026-10-14T09-33-55", [0, -1, 0], [], [100] },
            { "wf_act_bot_talk_to_avatar", "2026-10-20T17-39-35", [0, -1, 0], [], [100, 0] },
            { "wf_act_bot_teleport", "2026-10-20T17-39-35", [], [100], [100] },
            { "wf_act_call_stacks", "2026-10-20T17-39-35", [], [100], [] },
            { "wf_act_cancel_transaction", "2026-10-20T17-39-35", [0], [100], [0] },
            {
                "wf_act_change_var_val",
                "2026-10-20T14-17-07",
                [1, 0, 0, 0, 0, -10, 0],
                [0, 200],
                [0, 200]
            },
            { "wf_act_chase", "2026-10-16T06-06-32", [], [100], [] },
            { "wf_act_click_conf", "2026-10-20T17-39-35", [0, 0], [], [0] },
            { "wf_act_control_clock", "2026-10-16T06-07-41", [0], [100], [] },
            { "wf_act_flee", "2026-10-20T17-39-35", [], [100], [] },
            { "wf_act_freeze", "2026-10-20T17-39-35", [0, 0], [], [0] },
            { "wf_act_furni_to_furni", "2026-10-20T17-39-35", [], [0, 100], [] },
            { "wf_act_furni_to_user", "2026-10-20T17-39-35", [], [100], [0] },
            {
                "wf_act_give_currency",
                "2026-10-20T17-39-35",
                [0, 1, 0, -20, 1, 11],
                [100, 200],
                [0, 200]
            },
            {
                "wf_act_give_furni",
                "2026-10-20T17-39-35",
                [0, 1, 0, -20, 1, 0],
                [100, 200],
                [0, 200]
            },
            { "wf_act_give_score", "2026-10-20T17-39-35", [5, 0], [], [0] },
            { "wf_act_give_score_tm", "2026-10-20T17-39-35", [5, 0, 1], [], [] },
            {
                "wf_act_give_var",
                "2026-10-20T14-20-32",
                [1, 0, 0, 0, 0, -10, 0],
                [0, 200],
                [0, 200]
            },
            {
                "wf_act_init_transaction",
                "2026-10-14T09-18-49",
                [0, 10, 0, -20, 0, 300],
                [100, 101, 200],
                [0, 200]
            },
            { "wf_act_join_team", "2026-10-20T17-39-35", [1, 0], [], [0] },
            { "wf_act_kick_user", "2026-10-20T17-39-35", [], [], [0] },
            { "wf_act_leave_team", "2026-10-20T17-39-35", [], [], [0] },
            { "wf_act_log", "2026-10-20T17-39-35", [0], [], [] },
            { "wf_act_match_to_sshot", "2026-10-20T17-39-35", [0, 0, 0, 0, 1], [100], [] },
            { "wf_act_move_furni_as_group", "2026-10-20T17-39-35", [1, 0, 0], [100, 0], [0] },
            { "wf_act_move_rotate", "2026-10-20T17-39-35", [0, 0, 1], [100], [] },
            { "wf_act_move_rotate_user", "2026-10-14T08-33-56", [-1, -1], [], [0] },
            { "wf_act_move_to_dir", "2026-10-14T08-45-28", [0, 0, 1], [100], [] },
            { "wf_act_neg_call_stacks", "2026-10-18T18-03-32", [], [100], [] },
            { "wf_act_neg_log", "2026-10-20T17-39-35", [0], [], [] },
            { "wf_act_neg_send_signal", "2026-10-20T17-39-35", [0, 0], [100, 200], [200] },
            {
                "wf_act_place_furni",
                "2026-10-14T08-30-35",
                [0, 0, 0, 0, 0, 0, 0, 0, 0, -20],
                [110, 0, 200],
                [0, 200]
            },
            { "wf_act_rel_mov", "2026-10-20T17-39-35", [0, 0], [100], [] },
            { "wf_act_remove_furni", "2026-10-20T17-39-35", [], [200], [] },
            { "wf_act_remove_var", "2026-10-20T17-39-35", [1], [100], [0] },
            { "wf_act_reset_timers", "2026-10-20T17-39-35", [], [], [] },
            { "wf_act_send_signal", "2026-10-14T09-04-34", [0, 0], [100, 200], [200] },
            { "wf_act_set_altitude", "2026-10-20T17-39-35", [0, 0], [100], [] },
            {
                "wf_act_set_highlight",
                "2026-10-20T13-52-54",
                [0, 0, 100, 16771584, 0, 4, 0],
                [100],
                [0, 0]
            },
            { "wf_act_set_visibility", "2026-10-20T13-52-54", [0, 100, 0, 4, 0], [100], [0, 0] },
            { "wf_act_show_message", "2026-10-20T16-59-30", [0, 202, -1], [], [0] },
            { "wf_act_teleport_to", "2026-10-18T18-09-27", [0], [100], [0] },
            { "wf_act_teleport_to_room", "2026-10-20T17-39-35", [], [100], [0] },
            { "wf_act_toggle_state", "2026-10-18T18-03-32", [0], [100], [] },
            { "wf_act_toggle_to_rnd", "2026-10-20T17-39-35", [], [100], [] },
            { "wf_act_unfreeze", "2026-10-20T17-39-35", [], [], [0] },
            { "wf_act_user_to_furni", "2026-10-20T17-39-35", [0], [100], [0] },
            { "wf_cnd_actor_dir", "2026-10-20T17-37-19", [0], [], [0] },
            { "wf_cnd_actor_in_group", "2026-10-20T17-37-19", [], [], [0] },
            { "wf_cnd_actor_in_team", "2026-10-20T17-37-19", [0], [], [0] },
            {
                "wf_cnd_chest_has_item_type",
                "2026-10-20T17-37-19",
                [0, 0, -20, 2],
                [100, 101, 0],
                [0]
            },
            { "wf_cnd_chest_has_items", "2026-10-20T17-37-19", [0, 0, -20, 2], [100, 200], [200] },
            { "wf_cnd_counter_time_matches", "2026-10-20T17-37-19", [0, 0, 0, 0], [100], [] },
            { "wf_cnd_date_rng_active", "2026-10-20T17-37-19", [0, 0], [], [] },
            { "wf_cnd_furnis_hv_avtrs", "2026-10-18T18-00-25", [1], [100], [] },
            { "wf_cnd_has_altitude", "2026-10-20T17-37-19", [0, 0], [100], [] },
            { "wf_cnd_has_furni_on", "2026-10-20T17-37-19", [0], [100], [] },
            { "wf_cnd_has_handitem", "2026-10-20T17-37-19", [2], [], [0] },
            { "wf_cnd_has_var", "2026-10-18T19-07-44", [1], [100], [0] },
            { "wf_cnd_match_date", "2026-10-20T17-37-19", [0, 0, 127, 1, 1, 4095, 0, 0], [], [] },
            { "wf_cnd_match_snapshot", "2026-10-18T19-07-08", [0, 0, 0, 0], [100], [] },
            { "wf_cnd_match_time", "2026-10-20T17-37-19", [0, 0, 0, 0, 0, 0, 0, 0, 0], [], [] },
            { "wf_cnd_neg_has_var", "2026-10-18T19-07-44", [1], [100], [0] },
            { "wf_cnd_not_furni_on", "2026-10-20T17-37-19", [0], [100], [] },
            { "wf_cnd_not_has_handitem", "2026-10-20T17-37-19", [2], [], [0] },
            { "wf_cnd_not_hv_avtrs", "2026-10-18T18-06-24", [0], [100], [] },
            { "wf_cnd_not_in_group", "2026-10-20T17-37-19", [], [], [0] },
            { "wf_cnd_not_in_team", "2026-10-20T17-37-19", [0], [], [0] },
            { "wf_cnd_not_match_snap", "2026-10-18T19-06-51", [0, 0, 0, 0], [100], [] },
            { "wf_cnd_not_stuff_is", "2026-10-18T19-07-26", [], [0, 100], [] },
            { "wf_cnd_not_trggrer_on", "2026-10-20T17-37-19", [], [100], [0] },
            { "wf_cnd_not_triggerer_match", "2026-10-20T17-37-19", [1], [], [0, 101] },
            { "wf_cnd_not_user_count", "2026-10-20T17-37-19", [1, 50], [], [] },
            { "wf_cnd_not_user_performs_action", "2026-10-20T17-37-19", [0], [], [0] },
            { "wf_cnd_slc_quantity", "2026-10-20T17-37-19", [0, 5, 0], [200], [200] },
            { "wf_cnd_stuff_is", "2026-10-18T19-07-44", [], [0, 100], [] },
            { "wf_cnd_team_has_rank", "2026-10-20T17-37-19", [0, 0], [], [0] },
            { "wf_cnd_team_has_score", "2026-10-20T17-37-19", [0, 0, 0], [], [0] },
            { "wf_cnd_time_less_than", "2026-10-20T17-37-19", [20], [], [] },
            { "wf_cnd_time_more_than", "2026-10-20T17-37-19", [20], [], [] },
            { "wf_cnd_trggrer_on_frn", "2026-10-20T17-37-19", [], [100], [0] },
            { "wf_cnd_triggerer_match", "2026-10-20T17-37-19", [1], [], [0, 101] },
            { "wf_cnd_user_count_in", "2026-10-20T17-37-19", [1, 50], [], [] },
            { "wf_cnd_user_performs_action", "2026-10-20T17-37-19", [0], [], [0] },
            { "wf_cnd_valid_moves", "2026-10-20T17-37-19", [], [], [] },
            {
                "wf_cnd_var_age_match",
                "2026-10-20T17-37-19",
                [1, 0, 0, 0, 0, 1, 0, -10],
                [0, 200],
                [0, 200]
            },
            {
                "wf_cnd_var_val_match",
                "2026-10-14T09-08-22",
                [1, 2, 0, 0, 0, -10, 0],
                [0, 200],
                [0, 200]
            },
            { "wf_slc_furni_altitude", "2026-10-20T17-33-23", [0, 0], [], [] },
            { "wf_slc_furni_area", "2026-10-16T07-03-25", [0, 0, 0, 0], [], [] },
            { "wf_slc_furni_bytype", "2026-10-20T17-33-23", [0], [100], [] },
            {
                "wf_slc_furni_neighborhood",
                "2026-10-20T17-33-23",
                [1, 0, 0, 33554431, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                [100],
                [0]
            },
            { "wf_slc_furni_onfurni", "2026-10-20T17-33-23", [0], [100], [] },
            { "wf_slc_furni_picks", "2026-10-20T17-33-23", [], [], [] },
            { "wf_slc_furni_signal", "2026-10-20T17-33-23", [], [], [] },
            { "wf_slc_furni_with_var", "2026-10-16T06-03-41", [2, 0, 0, 0, -10, 0], [100], [0] },
            { "wf_slc_remote", "2026-10-20T17-33-23", [0, 0], [100], [] },
            { "wf_slc_users_area", "2026-10-20T17-33-23", [0, 0, 0, 0], [], [] },
            { "wf_slc_users_byaction", "2026-10-20T17-33-23", [0], [], [] },
            { "wf_slc_users_byname", "2026-10-16T07-03-25", [], [], [] },
            { "wf_slc_users_bytype", "2026-10-14T08-34-41", [1], [], [] },
            { "wf_slc_users_group", "2026-10-20T17-33-23", [], [], [] },
            { "wf_slc_users_handitem", "2026-10-20T17-33-23", [2], [], [] },
            {
                "wf_slc_users_neighborhood",
                "2026-10-20T17-33-23",
                [1, 0, 0, 33554431, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                [100],
                [0]
            },
            { "wf_slc_users_onfurni", "2026-10-20T17-33-23", [], [100], [] },
            { "wf_slc_users_signal", "2026-10-20T17-33-23", [], [], [] },
            { "wf_slc_users_team", "2026-10-20T17-33-23", [0], [], [] },
            { "wf_slc_users_with_var", "2026-10-16T06-11-58", [2, 0, 0, 0, -10, 0], [100], [0] },
            { "wf_trg_at_given_time", "2026-10-20T17-31-49", [1], [], [] },
            { "wf_trg_bot_reached_avtr", "2026-10-14T09-33-42", [], [], [100] },
            { "wf_trg_bot_reached_stf", "2026-10-20T17-31-49", [], [100], [100] },
            { "wf_trg_click_furni", "2026-10-16T06-07-41", [], [100], [] },
            { "wf_trg_click_tile", "2026-10-20T17-31-49", [], [100], [] },
            { "wf_trg_click_user", "2026-10-20T17-31-49", [1, 0], [], [] },
            { "wf_trg_clock_counter", "2026-10-20T17-31-49", [0, 0, 0], [100], [] },
            { "wf_trg_collision", "2026-10-20T17-31-49", [], [], [] },
            { "wf_trg_enter_room", "2026-10-20T17-08-36", [], [], [] },
            { "wf_trg_game_ends", "2026-10-20T17-31-49", [], [], [] },
            { "wf_trg_game_starts", "2026-10-20T17-31-49", [], [], [] },
            { "wf_trg_leave_room", "2026-10-20T17-31-49", [], [], [] },
            { "wf_trg_period_long", "2026-10-20T17-31-49", [10], [], [] },
            { "wf_trg_period_short", "2026-10-20T17-31-49", [4], [], [] },
            { "wf_trg_periodically", "2026-10-18T09-21-10", [1], [], [] },
            { "wf_trg_recv_signal", "2026-10-14T09-04-34", [], [100], [] },
            { "wf_trg_room_initializes", "2026-10-20T14-07-54", [], [], [] },
            { "wf_trg_says_something", "2026-10-14T09-41-34", [0, 0, 1], [], [] },
            { "wf_trg_score_achieved", "2026-10-20T17-31-49", [100, 0], [], [] },
            { "wf_trg_state_changed", "2026-10-14T08-45-28", [], [100], [] },
            { "wf_trg_stuff_state", "2026-10-20T17-31-49", [1], [100], [] },
            { "wf_trg_transaction_complete", "2026-10-20T17-31-49", [], [100], [] },
            { "wf_trg_transaction_fail", "2026-10-20T17-31-49", [], [100], [] },
            { "wf_trg_user_performs_action", "2026-10-20T17-31-49", [0], [], [] },
            { "wf_trg_var_changed", "2026-10-16T06-11-58", [1, 1, 0, 7, -1, 0], [], [] },
            { "wf_trg_walks_off_furni", "2026-10-20T17-31-49", [], [100], [] },
            { "wf_trg_walks_on_furni", "2026-10-18T18-00-25", [], [100], [] },
            { "wf_var_context", "2026-10-20T14-07-54", [0], [], [] },
            { "wf_var_echo", "2026-10-20T17-34-27", [], [], [] },
            { "wf_var_furni", "2026-10-20T17-02-40", [0, 1], [], [] },
            { "wf_var_pointer", "2026-10-20T17-03-14", [1], [], [] },
            { "wf_var_reference", "2026-10-20T17-06-11", [1], [], [] },
            { "wf_var_room", "2026-10-20T17-03-14", [1], [], [] },
            { "wf_var_user", "2026-10-14T09-36-01", [0, 0], [], [] },
            { "wf_xtra_anim_time", "2026-10-20T17-35-39", [500], [], [] },
            {
                "wf_xtra_custom_contract",
                "2026-10-14T08-57-35",
                [0, 0, 0, 1, -20, 0, 0, 0, 1, -20],
                [0, 0, 200, 200],
                [200, 200]
            },
            { "wf_xtra_exec_in_order", "2026-10-14T09-10-17", [], [], [] },
            { "wf_xtra_execution_limit", "2026-10-20T17-35-39", [100, 1], [], [] },
            { "wf_xtra_filter_furni", "2026-10-16T06-10-00", [10, 0, 1], [100], [0] },
            { "wf_xtra_filter_furni_by_var", "2026-10-20T17-35-39", [10, 0, 0, 1], [100], [0] },
            { "wf_xtra_filter_users", "2026-10-20T17-35-39", [10, 0, 1], [100], [0] },
            { "wf_xtra_filter_users_by_var", "2026-10-14T09-35-43", [10, 0, 0, 1], [100], [0] },
            { "wf_xtra_mov_carry_users", "2026-10-20T17-35-39", [0], [], [900] },
            { "wf_xtra_mov_curve", "2026-10-20T17-35-39", [0, 160, -20], [100], [0] },
            { "wf_xtra_mov_no_animation", "2026-10-20T17-35-39", [], [], [] },
            { "wf_xtra_mov_physics", "2026-10-14T08-47-48", [1, 0, 0, 0], [900, 900], [900] },
            { "wf_xtra_random", "2026-10-16T06-09-01", [0, 1], [], [] },
            {
                "wf_xtra_rotate_to_dir",
                "2026-10-20T17-35-39",
                [1, 1, 0, 0, 100, -20, 1, 1, 0, 4, 0, 0, 1, 1, 0, 0, 1, -20, 50],
                [901, 0, 0],
                [0, 0, 0]
            },
            { "wf_xtra_scan_chest_furni_by_type", "2026-10-20T17-35-39", [0], [0, 100], [] },
            { "wf_xtra_text_input_variable", "2026-10-14T09-41-34", [0], [], [] },
            { "wf_xtra_text_output_furni_name", "2026-10-20T17-35-39", [0], [0], [] },
            { "wf_xtra_text_output_username", "2026-10-14T09-43-08", [0], [], [0] },
            { "wf_xtra_text_output_variable", "2026-10-20T16-59-30", [0, 1, 0], [100], [0] },
            { "wf_xtra_unseen", "2026-10-20T17-35-39", [], [], [] },
            { "wf_xtra_var_import", "2026-10-20T14-15-30", [1, 1, 1], [], [] },
            {
                "wf_xtra_var_leaderboard",
                "2026-10-16T06-03-41",
                [0, 2, 0, 1, -1, -1, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0],
                [],
                []
            },
            { "wf_xtra_var_lvlup_system", "2026-10-20T17-35-39", [0, 1, 100, 50, 50], [], [] },
            { "wf_xtra_var_share", "2026-10-20T17-13-00", [1, 1, 1], [], [] },
            { "wf_xtra_var_text_connector", "2026-10-20T17-35-39", [], [], [] },
            { "wf_xtra_var_time_util", "2026-10-20T17-35-39", [0, 1], [], [] },
            {
                "wf_xtra_varfx_boss",
                "2026-10-20T17-35-39",
                [1, 2, 0, 7, 0, 3000, 0, -1, -1, -1, 0, 0, 0, 100, 0, 0, 1, 1, 0, 0, 0, 0, 0],
                [],
                []
            },
            {
                "wf_xtra_varfx_hp",
                "2026-10-14T09-44-13",
                [1, 2, 0, 7, 0, 3000, 0, -1, -1, -1, 0, 0, 0, 100, 0, 0, 1, 1, 0, 0, 0, 0, 0],
                [],
                []
            },
            {
                "wf_xtra_varfx_levelling",
                "2026-10-20T17-35-39",
                [1, 2, 0, 7, 0, 3000, 0, -1, -1, -1, 0, 0, 0, 100, 0, 0, 1, 1, 0, 0, 0, 2, 0],
                [],
                []
            },
            {
                "wf_xtra_varfx_number",
                "2026-10-20T17-35-39",
                [1, 2, 0, 7, 0, 3000, 0, -1, -1, -1, 0, 0, 0, 100, 0, 0, 1, 1, 0, 0, 0, 0, 0],
                [],
                []
            },
            {
                "wf_xtra_varfx_prog",
                "2026-10-20T17-35-39",
                [1, 2, 0, 7, 0, 3000, 0, -1, -1, -1, 0, 0, 0, 100, 0, 0, 1, 1, 0, 0, 0, 0, 0],
                [],
                []
            },
            {
                "wf_xtra_varfx_status",
                "2026-10-20T17-35-39",
                [1, 2, 0, 7, 0, 3000, 0, -1, -1, -1, 0, 0, 0, 100, 0, 0, 1, 1, 0, 0, 0, 0, 0],
                [],
                []
            },
        };

    [Theory]
    [MemberData(nameof(OfficialFreshBoxes))]
    public async Task A_fresh_box_opens_like_the_official_one(
        string classname,
        string recording,
        int[] intParams,
        int[] furniSources,
        int[] userSources
    )
    {
        _ = recording;

        var box = AddBox(new WiredRoom(8, 8), classname);

        await box.LoadWiredAsync(Ct);

        var snapshot = box.GetSnapshot();

        snapshot.DefaultIntParams.Should().Equal(intParams);
        snapshot.IntParams.Should().Equal(intParams);
        snapshot
            .DefaultFurniSources.Select(x =>
                (int)WiredFurniSourceTypeExtensions.GetProtocolId(x[0])
            )
            .Should()
            .Equal(furniSources);
        snapshot
            .DefaultPlayerSources.Select(x =>
                (int)WiredPlayerSourceTypeExtensions.GetProtocolId(x[0])
            )
            .Should()
            .Equal(userSources);
    }

    [Fact]
    public async Task A_fresh_conditions_evaluation_asks_for_at_least_one()
    {
        var box = AddBox(new WiredRoom(8, 8), "wf_xtra_or_eval");

        await box.LoadWiredAsync(Ct);

        box.GetSnapshot().IntParams.Should().Equal(1, 0, 0);
    }

    private static FurnitureWiredLogic AddBox(WiredRoom room, string classname)
    {
        var type = typeof(FurnitureWiredLogic)
            .Assembly.GetTypes()
            .Single(type =>
                type.GetCustomAttribute<RoomObjectLogicAttribute>(false)?.Key == classname
            );

        return (FurnitureWiredLogic)
            typeof(WiredRoom)
                .GetMethod(nameof(WiredRoom.AddBox))!
                .MakeGenericMethod(type)
                .Invoke(room, [BOX, 0, 0, classname])!;
    }
}
