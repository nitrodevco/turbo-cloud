using FluentAssertions;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The editor lists internal variables by id, and the id is built from where a variable sits.
/// These are the lists the official client's Creator Tools showed on 2026-10-08 (full names from
/// the picker's tooltips), top to bottom (smart variables first; <c>@level</c>, the avatar level, is the one this hotel
/// does not have).
/// </summary>
public sealed class WiredOfficialVariableOrderTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public void The_furni_variables_are_listed_as_the_official_client_lists_them()
    {
        Listed(WiredVariableTargetType.Furni, startsWith: "~clock")
            .Should()
            .Equal(
                "~clock.is_game_aware",
                "~clock.pulse_count",
                "~clock.state",
                "@id",
                "@class_id",
                "@height",
                "@state",
                "@position.x",
                "@position.y",
                "@rotation",
                "@altitude",
                "@is_invisible",
                "@position",
                "@occupation",
                "@type",
                "@is_stackable",
                "@can_stand_on",
                "@can_sit_on",
                "@can_lay_on",
                "@dimensions.x",
                "@dimensions.y",
                "@owner_id",
                "@wallitem_offset",
                "@projectile.animation.tiles_traveled",
                "@projectile.animation.user_collisions",
                "@projectile.animation.furni_collisions",
                "@projectile.animation.position.x",
                "@projectile.animation.position.y",
                "@projectile.animation.position.altitude",
                "@projectile.animation.is_traveling"
            );
    }

    [Fact]
    public void The_user_variables_are_listed_as_the_official_client_lists_them()
    {
        Listed(WiredVariableTargetType.User, startsWith: "@")
            .Should()
            .Equal(
                "@index",
                "@type",
                "@gender",
                "@achievement_score",
                "@is_hc",
                "@has_rights",
                "@is_group_admin",
                "@is_owner",
                "@position.x",
                "@position.y",
                "@direction",
                "@altitude",
                "@position",
                "@team.score",
                "@team.color",
                "@team.type",
                "@handitem",
                "@effect",
                "@is_frozen",
                "@is_muted",
                "@is_trading",
                "@favourite_group_id",
                "@dance",
                "@sign",
                "@is_idle",
                "@room_entry.method",
                "@room_entry.teleport_id",
                "@user_id",
                "@pet_id",
                "@bot_id"
            );
    }

    [Fact]
    public void The_global_variables_are_listed_as_the_official_client_lists_them()
    {
        Listed(WiredVariableTargetType.Global, startsWith: "@")
            .Should()
            .Equal(
                "@furni_count",
                "@user_count",
                "@wired_timer",
                "@teams.red.score",
                "@teams.green.score",
                "@teams.blue.score",
                "@teams.yellow.score",
                "@teams.red.size",
                "@teams.green.size",
                "@teams.blue.size",
                "@teams.yellow.size",
                "@room_id",
                "@group_id",
                "@current_time",
                "@current_time.milliseconds_of_seconds",
                "@current_time.seconds_of_minute",
                "@current_time.minute_of_hour",
                "@current_time.hour_of_day",
                "@current_time.day_of_week",
                "@current_time.day_of_month",
                "@current_time.day_of_year",
                "@current_time.week_of_year",
                "@current_time.month_of_year",
                "@current_time.year"
            );
    }

    [Fact]
    public void The_context_variables_are_listed_as_the_official_client_lists_them()
    {
        Listed(WiredVariableTargetType.Context, startsWith: "@")
            .Should()
            .Equal(
                "@selector_furni_count",
                "@selector_user_count",
                "@signal_furni_count",
                "@signal_user_count",
                "@event.signal.antenna_id",
                "@event.chat.type",
                "@event.chat.style",
                "@event.link.source_room_id",
                "@event.variable_update.box_id",
                "@event.variable_update.change_type",
                "@event.variable_update.old_value",
                "@event.variable_update.new_value",
                "@event.variable_update.difference",
                "@event.variable_update.change_origin",
                "@event.transaction_complete.multiplier",
                "@event.transaction_complete.deposit.furni_count",
                "@event.transaction_complete.deposit.coins_count",
                "@event.transaction_complete.withdrawal.furni_count",
                "@event.transaction_complete.withdrawal.coins_count",
                "@event.transaction_failed.reason"
            );
    }

    /// <summary>
    /// Every internal variable of the target the server declares, in the editor's order. Smart
    /// variables of other furni and pets are left out, but for the ones named by
    /// <paramref name="startsWith"/>.
    /// </summary>
    private List<string> Listed(WiredVariableTargetType target, string startsWith) =>
        [
            .. typeof(WiredInternalVariable)
                .Assembly.GetTypes()
                .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(WiredInternalVariable)))
                .Select(t =>
                    (
                        (WiredInternalVariable)Activator.CreateInstance(t, _room.Harness.Room)!
                    ).GetVarSnapshot()
                )
                .Where(x =>
                    x.TargetType == target
                    && (x.VariableName.StartsWith('@') || x.VariableName.StartsWith(startsWith))
                )
                .OrderByDescending(x => x.VariableId.Value)
                .Select(x => x.VariableName),
        ];
}
