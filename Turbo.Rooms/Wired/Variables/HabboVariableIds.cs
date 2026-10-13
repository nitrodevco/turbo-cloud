using System.Collections.Frozen;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Variables;

/// <summary>
/// Habbo's fixed ids for the variables every room has, by target and name, exactly as hhus sends
/// them in WiredAllVariablesDiffs (recorded WIN63-202610121709, room 79874981). A variable missing
/// here keeps its hashed id.
/// </summary>
public static class HabboVariableIds
{
    private static readonly FrozenDictionary<
        (WiredVariableTargetType, string),
        WiredVariableId
    > Ids = new Dictionary<(WiredVariableTargetType, string), WiredVariableId>
    {
        [(WiredVariableTargetType.Furni, "@id")] = WiredVariableId.BuiltIn(100),
        [(WiredVariableTargetType.Furni, "@class_id")] = WiredVariableId.BuiltIn(101),
        [(WiredVariableTargetType.Furni, "@height")] = WiredVariableId.BuiltIn(102),
        [(WiredVariableTargetType.Furni, "@state")] = WiredVariableId.BuiltIn(110),
        [(WiredVariableTargetType.Furni, "@position.x")] = WiredVariableId.BuiltIn(120),
        [(WiredVariableTargetType.Furni, "@position.y")] = WiredVariableId.BuiltIn(121),
        [(WiredVariableTargetType.Furni, "@rotation")] = WiredVariableId.BuiltIn(122),
        [(WiredVariableTargetType.Furni, "@altitude")] = WiredVariableId.BuiltIn(123),
        [(WiredVariableTargetType.Furni, "@is_invisible")] = WiredVariableId.BuiltIn(124),
        [(WiredVariableTargetType.Furni, "@position")] = WiredVariableId.BuiltIn(125),
        [(WiredVariableTargetType.Furni, "@occupation")] = WiredVariableId.BuiltIn(126),
        [(WiredVariableTargetType.Furni, "@type")] = WiredVariableId.BuiltIn(129),
        [(WiredVariableTargetType.Furni, "@is_stackable")] = WiredVariableId.BuiltIn(130),
        [(WiredVariableTargetType.Furni, "@can_stand_on")] = WiredVariableId.BuiltIn(131),
        [(WiredVariableTargetType.Furni, "@can_sit_on")] = WiredVariableId.BuiltIn(132),
        [(WiredVariableTargetType.Furni, "@can_lay_on")] = WiredVariableId.BuiltIn(133),
        [(WiredVariableTargetType.Furni, "@dimensions.x")] = WiredVariableId.BuiltIn(140),
        [(WiredVariableTargetType.Furni, "@dimensions.y")] = WiredVariableId.BuiltIn(141),
        [(WiredVariableTargetType.Furni, "@owner_id")] = WiredVariableId.BuiltIn(180),
        [(WiredVariableTargetType.Furni, "@wallitem_offset")] = WiredVariableId.BuiltIn(190),
        [(WiredVariableTargetType.User, "@index")] = WiredVariableId.BuiltIn(200),
        [(WiredVariableTargetType.User, "@type")] = WiredVariableId.BuiltIn(205),
        [(WiredVariableTargetType.User, "@gender")] = WiredVariableId.BuiltIn(206),
        [(WiredVariableTargetType.User, "@level")] = WiredVariableId.BuiltIn(210),
        [(WiredVariableTargetType.User, "@achievement_score")] = WiredVariableId.BuiltIn(211),
        [(WiredVariableTargetType.User, "@is_hc")] = WiredVariableId.BuiltIn(220),
        [(WiredVariableTargetType.User, "@has_rights")] = WiredVariableId.BuiltIn(221),
        [(WiredVariableTargetType.User, "@is_group_admin")] = WiredVariableId.BuiltIn(222),
        [(WiredVariableTargetType.User, "@is_owner")] = WiredVariableId.BuiltIn(223),
        [(WiredVariableTargetType.User, "@position.x")] = WiredVariableId.BuiltIn(230),
        [(WiredVariableTargetType.User, "@position.y")] = WiredVariableId.BuiltIn(231),
        [(WiredVariableTargetType.User, "@direction")] = WiredVariableId.BuiltIn(232),
        [(WiredVariableTargetType.User, "@altitude")] = WiredVariableId.BuiltIn(233),
        [(WiredVariableTargetType.User, "@position")] = WiredVariableId.BuiltIn(234),
        [(WiredVariableTargetType.User, "@team.score")] = WiredVariableId.BuiltIn(237),
        [(WiredVariableTargetType.User, "@team.color")] = WiredVariableId.BuiltIn(238),
        [(WiredVariableTargetType.User, "@team.type")] = WiredVariableId.BuiltIn(239),
        [(WiredVariableTargetType.User, "@handitem")] = WiredVariableId.BuiltIn(240),
        [(WiredVariableTargetType.User, "@effect")] = WiredVariableId.BuiltIn(241),
        [(WiredVariableTargetType.User, "@is_frozen")] = WiredVariableId.BuiltIn(242),
        [(WiredVariableTargetType.User, "@is_muted")] = WiredVariableId.BuiltIn(243),
        [(WiredVariableTargetType.User, "@is_trading")] = WiredVariableId.BuiltIn(244),
        [(WiredVariableTargetType.User, "@habbicon")] = WiredVariableId.BuiltIn(245),
        [(WiredVariableTargetType.User, "@favourite_group_id")] = WiredVariableId.BuiltIn(250),
        [(WiredVariableTargetType.User, "@dance")] = WiredVariableId.BuiltIn(260),
        [(WiredVariableTargetType.User, "@sign")] = WiredVariableId.BuiltIn(261),
        [(WiredVariableTargetType.User, "@is_idle")] = WiredVariableId.BuiltIn(262),
        [(WiredVariableTargetType.User, "@room_entry.method")] = WiredVariableId.BuiltIn(266),
        [(WiredVariableTargetType.User, "@room_entry.teleport_id")] = WiredVariableId.BuiltIn(267),
        [(WiredVariableTargetType.User, "@user_id")] = WiredVariableId.BuiltIn(270),
        [(WiredVariableTargetType.User, "@pet_id")] = WiredVariableId.BuiltIn(271),
        [(WiredVariableTargetType.User, "@bot_id")] = WiredVariableId.BuiltIn(272),
        [(WiredVariableTargetType.User, "@pet_owner_id")] = WiredVariableId.BuiltIn(280),
        [(WiredVariableTargetType.User, "@transaction.in_trade")] = WiredVariableId.BuiltIn(281),
        [(WiredVariableTargetType.User, "@transaction.start_time")] = WiredVariableId.BuiltIn(282),
        [(WiredVariableTargetType.User, "@transaction.contract_id")] = WiredVariableId.BuiltIn(283),
        [(WiredVariableTargetType.User, "@transaction.state")] = WiredVariableId.BuiltIn(284),
        [(WiredVariableTargetType.User, "@transaction.can_accept")] = WiredVariableId.BuiltIn(285),
        [(WiredVariableTargetType.User, "@transaction.current_multiplier")] =
            WiredVariableId.BuiltIn(286),
        [(WiredVariableTargetType.Global, "@furni_count")] = WiredVariableId.BuiltIn(300),
        [(WiredVariableTargetType.Global, "@user_count")] = WiredVariableId.BuiltIn(301),
        [(WiredVariableTargetType.Global, "@wired_timer")] = WiredVariableId.BuiltIn(310),
        [(WiredVariableTargetType.Global, "@teams.red.score")] = WiredVariableId.BuiltIn(320),
        [(WiredVariableTargetType.Global, "@teams.green.score")] = WiredVariableId.BuiltIn(321),
        [(WiredVariableTargetType.Global, "@teams.blue.score")] = WiredVariableId.BuiltIn(322),
        [(WiredVariableTargetType.Global, "@teams.yellow.score")] = WiredVariableId.BuiltIn(323),
        [(WiredVariableTargetType.Global, "@teams.red.size")] = WiredVariableId.BuiltIn(330),
        [(WiredVariableTargetType.Global, "@teams.green.size")] = WiredVariableId.BuiltIn(331),
        [(WiredVariableTargetType.Global, "@teams.blue.size")] = WiredVariableId.BuiltIn(332),
        [(WiredVariableTargetType.Global, "@teams.yellow.size")] = WiredVariableId.BuiltIn(333),
        [(WiredVariableTargetType.Global, "@room_id")] = WiredVariableId.BuiltIn(380),
        [(WiredVariableTargetType.Global, "@group_id")] = WiredVariableId.BuiltIn(381),
        [(WiredVariableTargetType.Context, "@selector_furni_count")] = WiredVariableId.BuiltIn(400),
        [(WiredVariableTargetType.Context, "@selector_user_count")] = WiredVariableId.BuiltIn(401),
        [(WiredVariableTargetType.Context, "@signal_furni_count")] = WiredVariableId.BuiltIn(410),
        [(WiredVariableTargetType.Context, "@signal_user_count")] = WiredVariableId.BuiltIn(411),
        [(WiredVariableTargetType.Context, "@event.signal.antenna_id")] = WiredVariableId.BuiltIn(
            420
        ),
        [(WiredVariableTargetType.Context, "@event.chat.type")] = WiredVariableId.BuiltIn(450),
        [(WiredVariableTargetType.Context, "@event.chat.style")] = WiredVariableId.BuiltIn(451),
        [(WiredVariableTargetType.Context, "@event.habbicon")] = WiredVariableId.BuiltIn(452),
        [(WiredVariableTargetType.Context, "@event.link.source_room_id")] = WiredVariableId.BuiltIn(
            453
        ),
        [(WiredVariableTargetType.Context, "@event.variable_update.box_id")] =
            WiredVariableId.BuiltIn(459),
        [(WiredVariableTargetType.Context, "@event.variable_update.change_type")] =
            WiredVariableId.BuiltIn(460),
        [(WiredVariableTargetType.Context, "@event.variable_update.old_value")] =
            WiredVariableId.BuiltIn(461),
        [(WiredVariableTargetType.Context, "@event.variable_update.new_value")] =
            WiredVariableId.BuiltIn(462),
        [(WiredVariableTargetType.Context, "@event.variable_update.difference")] =
            WiredVariableId.BuiltIn(463),
        [(WiredVariableTargetType.Context, "@event.variable_update.change_origin")] =
            WiredVariableId.BuiltIn(464),
        [(WiredVariableTargetType.Context, "@event.transaction_complete.multiplier")] =
            WiredVariableId.BuiltIn(470),
        [(WiredVariableTargetType.Context, "@event.transaction_complete.deposit.furni_count")] =
            WiredVariableId.BuiltIn(471),
        [(WiredVariableTargetType.Context, "@event.transaction_complete.deposit.coins_count")] =
            WiredVariableId.BuiltIn(472),
        [(WiredVariableTargetType.Context, "@event.transaction_complete.withdrawal.furni_count")] =
            WiredVariableId.BuiltIn(473),
        [(WiredVariableTargetType.Context, "@event.transaction_complete.withdrawal.coins_count")] =
            WiredVariableId.BuiltIn(474),
        [(WiredVariableTargetType.Context, "@event.transaction_failed.reason")] =
            WiredVariableId.BuiltIn(478),
        [(WiredVariableTargetType.Global, "@current_time")] = WiredVariableId.BuiltIn(1000),
        [(WiredVariableTargetType.Global, "@current_time.milliseconds_of_seconds")] =
            WiredVariableId.BuiltIn(1001),
        [(WiredVariableTargetType.Global, "@current_time.seconds_of_minute")] =
            WiredVariableId.BuiltIn(1002),
        [(WiredVariableTargetType.Global, "@current_time.minute_of_hour")] =
            WiredVariableId.BuiltIn(1003),
        [(WiredVariableTargetType.Global, "@current_time.hour_of_day")] = WiredVariableId.BuiltIn(
            1004
        ),
        [(WiredVariableTargetType.Global, "@current_time.day_of_week")] = WiredVariableId.BuiltIn(
            1005
        ),
        [(WiredVariableTargetType.Global, "@current_time.day_of_month")] = WiredVariableId.BuiltIn(
            1006
        ),
        [(WiredVariableTargetType.Global, "@current_time.day_of_year")] = WiredVariableId.BuiltIn(
            1007
        ),
        [(WiredVariableTargetType.Global, "@current_time.week_of_year")] = WiredVariableId.BuiltIn(
            1008
        ),
        [(WiredVariableTargetType.Global, "@current_time.month_of_year")] = WiredVariableId.BuiltIn(
            1009
        ),
        [(WiredVariableTargetType.Global, "@current_time.year")] = WiredVariableId.BuiltIn(1010),
        [(WiredVariableTargetType.Furni, "@can_move_freely")] = WiredVariableId.BuiltIn(2000),
        [(WiredVariableTargetType.Furni, "@projectile.animation.tiles_travelled")] =
            WiredVariableId.BuiltIn(2100),
        [(WiredVariableTargetType.Furni, "@projectile.animation.user_collisions")] =
            WiredVariableId.BuiltIn(2101),
        [(WiredVariableTargetType.Furni, "@projectile.animation.furni_collisions")] =
            WiredVariableId.BuiltIn(2102),
        [(WiredVariableTargetType.Furni, "@projectile.animation.position.x")] =
            WiredVariableId.BuiltIn(2103),
        [(WiredVariableTargetType.Furni, "@projectile.animation.position.y")] =
            WiredVariableId.BuiltIn(2104),
        [(WiredVariableTargetType.Furni, "@projectile.animation.position.altitude")] =
            WiredVariableId.BuiltIn(2105),
        [(WiredVariableTargetType.Furni, "@projectile.animation.is_travelling")] =
            WiredVariableId.BuiltIn(2106),
        [(WiredVariableTargetType.Furni, "~area_hide.hiding_wallitems")] = WiredVariableId.Smart(
            115
        ),
        [(WiredVariableTargetType.Furni, "~area_hide.inverted")] = WiredVariableId.Smart(116),
        [(WiredVariableTargetType.Furni, "~area_hide.is_invisible_furni")] = WiredVariableId.Smart(
            114
        ),
        [(WiredVariableTargetType.Furni, "~area_hide.length")] = WiredVariableId.Smart(113),
        [(WiredVariableTargetType.Furni, "~area_hide.root_x")] = WiredVariableId.Smart(110),
        [(WiredVariableTargetType.Furni, "~area_hide.root_y")] = WiredVariableId.Smart(111),
        [(WiredVariableTargetType.Furni, "~area_hide.width")] = WiredVariableId.Smart(112),
        [(WiredVariableTargetType.Furni, "~background_color.hue")] = WiredVariableId.Smart(100),
        [(WiredVariableTargetType.Furni, "~background_color.lightness")] = WiredVariableId.Smart(
            102
        ),
        [(WiredVariableTargetType.Furni, "~background_color.saturation")] = WiredVariableId.Smart(
            101
        ),
        [(WiredVariableTargetType.Furni, "~chest.available_amount")] = WiredVariableId.Smart(130),
        [(WiredVariableTargetType.Furni, "~chest.capacity")] = WiredVariableId.Smart(133),
        [(WiredVariableTargetType.Furni, "~chest.is_auto_lock")] = WiredVariableId.Smart(132),
        [(WiredVariableTargetType.Furni, "~chest.is_donatable")] = WiredVariableId.Smart(135),
        [(WiredVariableTargetType.Furni, "~chest.is_open")] = WiredVariableId.Smart(134),
        [(WiredVariableTargetType.Furni, "~chest.locked")] = WiredVariableId.Smart(131),
        [(WiredVariableTargetType.Furni, "~clock.is_game_aware")] = WiredVariableId.Smart(122),
        [(WiredVariableTargetType.Furni, "~clock.pulse_count")] = WiredVariableId.Smart(121),
        [(WiredVariableTargetType.Furni, "~clock.state")] = WiredVariableId.Smart(120),
        [(WiredVariableTargetType.User, "~horse.controller_user_id")] = WiredVariableId.Smart(502),
        [(WiredVariableTargetType.User, "~horse.has_saddle")] = WiredVariableId.Smart(500),
        [(WiredVariableTargetType.User, "~horse.is_riding")] = WiredVariableId.Smart(501),
        [(WiredVariableTargetType.User, "~pet.creation_time")] = WiredVariableId.Smart(431),
        [(WiredVariableTargetType.User, "~pet.energy")] = WiredVariableId.Smart(402),
        [(WiredVariableTargetType.User, "~pet.experience")] = WiredVariableId.Smart(401),
        [(WiredVariableTargetType.User, "~pet.experience_required")] = WiredVariableId.Smart(411),
        [(WiredVariableTargetType.User, "~pet.happiness")] = WiredVariableId.Smart(403),
        [(WiredVariableTargetType.User, "~pet.level")] = WiredVariableId.Smart(400),
        [(WiredVariableTargetType.User, "~pet.max_energy")] = WiredVariableId.Smart(412),
        [(WiredVariableTargetType.User, "~pet.max_happiness")] = WiredVariableId.Smart(413),
        [(WiredVariableTargetType.User, "~pet.max_level")] = WiredVariableId.Smart(410),
        [(WiredVariableTargetType.User, "~pet.owner_id")] = WiredVariableId.Smart(430),
        [(WiredVariableTargetType.User, "~pet.scratches")] = WiredVariableId.Smart(420),
        [(WiredVariableTargetType.User, "~plant.can_breed")] = WiredVariableId.Smart(610),
        [(WiredVariableTargetType.User, "~plant.can_harvest")] = WiredVariableId.Smart(611),
        [(WiredVariableTargetType.User, "~plant.can_revive")] = WiredVariableId.Smart(612),
        [(WiredVariableTargetType.User, "~plant.color")] = WiredVariableId.Smart(602),
        [(WiredVariableTargetType.User, "~plant.is_dead")] = WiredVariableId.Smart(613),
        [(WiredVariableTargetType.User, "~plant.is_growing")] = WiredVariableId.Smart(614),
        [(WiredVariableTargetType.User, "~plant.max_wellbeing_seconds")] = WiredVariableId.Smart(
            617
        ),
        [(WiredVariableTargetType.User, "~plant.rarity_level")] = WiredVariableId.Smart(600),
        [(WiredVariableTargetType.User, "~plant.remaining_growing_seconds")] =
            WiredVariableId.Smart(616),
        [(WiredVariableTargetType.User, "~plant.remaining_wellbeing_seconds")] =
            WiredVariableId.Smart(615),
        [(WiredVariableTargetType.User, "~plant.shape")] = WiredVariableId.Smart(601),
        [(WiredVariableTargetType.Furni, "~recolorable_furni.color.rgb")] = WiredVariableId.Smart(
            146
        ),
        [(WiredVariableTargetType.Furni, "~recolorable_furni.color.rgb.blue")] =
            WiredVariableId.Smart(142),
        [(WiredVariableTargetType.Furni, "~recolorable_furni.color.rgb.green")] =
            WiredVariableId.Smart(141),
        [(WiredVariableTargetType.Furni, "~recolorable_furni.color.rgb.red")] =
            WiredVariableId.Smart(140),
        [(WiredVariableTargetType.Furni, "~teleport.target_id")] = WiredVariableId.Smart(103),
        [(WiredVariableTargetType.Furni, "~walk_helper.is_multi_walk")] = WiredVariableId.Smart(
            104
        ),
    }.ToFrozenDictionary();

    public static bool TryGet(
        WiredVariableTargetType targetType,
        string name,
        out WiredVariableId id
    ) => Ids.TryGetValue((targetType, name), out id);
}
