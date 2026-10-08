using System;
using System.Collections.Generic;
using System.Globalization;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Derives calendar and duration values from the variable box on its tile. Param 0 is a
/// bitmask: the low 16 bits pick calendar fields of the instant (bit n for field n:
/// milliseconds, seconds, minutes, hours, day of week, day of month, day of year, week,
/// month, year), the high 16 bits pick whole units counted from 1970 up to that instant
/// (<c>time_util.advanced_info</c>; bit 19+n for unit n: milliseconds, seconds, minutes,
/// hours, days, weeks, months), so two of them subtract to the time between two instants.
/// Param 1 chooses the
/// instant: the value as a unix timestamp, the creation time or the last update time.
/// </summary>
[RoomObjectLogic("wf_xtra_var_time_util")]
public class WiredAddonVariableTimeUtil(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSubVariableAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int MODE_VALUE = 0;
    private const int MODE_CREATED = 1;
    private const int MODE_UPDATED = 2;

    // The editor's SubVariableParam ids; a field is ticked when bit `id` of the mask is set.
    private static readonly (int bit, string name)[] CALENDAR_FIELDS =
    [
        (1, "milliseconds_of_seconds"),
        (2, "seconds_of_minute"),
        (3, "minute_of_hour"),
        (4, "hour_of_day"),
        (5, "day_of_week"),
        (6, "day_of_month"),
        (7, "day_of_year"),
        (8, "week_of_year"),
        (9, "month_of_year"),
        (10, "year"),
    ];

    private static readonly (int bit, string name)[] DURATION_FIELDS =
    [
        (20, "millisecond"),
        (21, "second"),
        (22, "minute"),
        (23, "hour"),
        (24, "day"),
        (25, "week"),
        (26, "month"),
    ];

    public override int WiredCode => (int)WiredAddonType.VARIABLE_TIME_UTIL;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [WiredRules.AnyInt(), new WiredRangeParamRule(0, 2, 0)];

    protected override IEnumerable<IWiredVariable> BuildSubVariables(
        FurnitureWiredVariableLogic parent,
        string parentName
    )
    {
        var mask = GetIntParamOrDefault(0, 0);
        var index = 0;

        foreach (var (bit, name) in CALENDAR_FIELDS)
        {
            var fieldIndex = index++;

            if ((mask & (1 << bit)) == 0)
                continue;

            var field = name;

            yield return Create(
                fieldIndex,
                parentName,
                field,
                parent,
                (p, key) => Calendar(p, key, field)
            );
        }

        foreach (var (bit, name) in DURATION_FIELDS)
        {
            var fieldIndex = index++;

            if ((mask & (1 << bit)) == 0)
                continue;

            var unit = name;

            yield return Create(
                fieldIndex,
                parentName,
                unit,
                parent,
                (p, key) => Duration(p, key, unit)
            );
        }
    }

    private DateTimeOffset? ResolveInstant(IWiredVariable parent, WiredVariableKey key)
    {
        switch (GetIntParamOrDefault(1, MODE_VALUE))
        {
            case MODE_CREATED:
            case MODE_UPDATED:
            {
                if (!parent.TryGetTimestamps(key, out var created, out var updated))
                    return null;

                var ms = GetIntParamOrDefault(1, MODE_VALUE) == MODE_CREATED ? created : updated;

                return ms <= 0 ? null : DateTimeOffset.FromUnixTimeMilliseconds(ms);
            }
            default:
                return parent.TryGetValue(key, out var value)
                    ? DateTimeOffset.FromUnixTimeSeconds(Math.Max(0, value.Value))
                    : null;
        }
    }

    private WiredVariableValue? Calendar(IWiredVariable parent, WiredVariableKey key, string field)
    {
        if (ResolveInstant(parent, key) is not { } instant)
            return null;

        var local = TimeZoneInfo.ConvertTime(instant, WiredSystem.GetRoomTimeZone());

        return field switch
        {
            "milliseconds_of_seconds" => local.Millisecond,
            "seconds_of_minute" => local.Second,
            "minute_of_hour" => local.Minute,
            "hour_of_day" => local.Hour,
            "day_of_week" => ((int)local.DayOfWeek + 6) % 7 + 1,
            "day_of_month" => local.Day,
            "day_of_year" => local.DayOfYear,
            "week_of_year" => ISOWeek.GetWeekOfYear(local.DateTime),
            "month_of_year" => local.Month,
            "year" => local.Year,
            _ => null,
        };
    }

    private WiredVariableValue? Duration(IWiredVariable parent, WiredVariableKey key, string unit)
    {
        if (ResolveInstant(parent, key) is not { } instant)
            return null;

        var ms = instant.ToUnixTimeMilliseconds();
        var utc = instant.UtcDateTime;
        long amount = unit switch
        {
            "millisecond" => ms,
            "second" => ms / 1_000,
            "minute" => ms / 60_000,
            "hour" => ms / 3_600_000,
            "day" => ms / 86_400_000,
            "week" => ms / 604_800_000,
            "month" => (utc.Year - 1970) * 12L + utc.Month - 1,
            _ => 0,
        };

        return amount;
    }
}
