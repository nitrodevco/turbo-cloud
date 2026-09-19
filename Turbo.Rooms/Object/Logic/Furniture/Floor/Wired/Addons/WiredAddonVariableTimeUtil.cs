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
/// bitmask: the low 16 bits pick calendar fields of the instant (bit n-1 for field n:
/// milliseconds, seconds, minutes, hours, day of week, day of month, day of year, week,
/// month, year), the high 16 bits pick duration units since that instant (bit 20+n-1 for
/// unit n: milliseconds, seconds, minutes, hours, days, weeks, months). Param 1 chooses the
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

    private static readonly (int bit, string name)[] CALENDAR_FIELDS =
    [
        (0, "milliseconds_of_second"),
        (1, "seconds_of_minute"),
        (2, "minute_of_hour"),
        (3, "hour_of_day"),
        (4, "day_of_week"),
        (5, "day_of_month"),
        (6, "day_of_year"),
        (7, "week_of_year"),
        (8, "month_of_year"),
        (9, "year"),
    ];

    private static readonly (int bit, string name)[] DURATION_FIELDS =
    [
        (19, "millisecond"),
        (20, "second"),
        (21, "minute"),
        (22, "hour"),
        (23, "day"),
        (24, "week"),
        (25, "month"),
    ];

    public override int WiredCode => (int)WiredAddonType.VARIABLE_TIME_UTIL;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredParamRule(0), new WiredRangeParamRule(0, 2, 0)];

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

        var local = TimeZoneInfo.ConvertTime(instant, _roomGrain.WiredSystem.GetRoomTimeZone());

        return field switch
        {
            "milliseconds_of_second" => local.Millisecond,
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

        var elapsed = DateTimeOffset.UtcNow - instant;
        double amount = unit switch
        {
            "millisecond" => elapsed.TotalMilliseconds,
            "second" => elapsed.TotalSeconds,
            "minute" => elapsed.TotalMinutes,
            "hour" => elapsed.TotalHours,
            "day" => elapsed.TotalDays,
            "week" => elapsed.TotalDays / 7,
            "month" => elapsed.TotalDays / 30,
            _ => 0,
        };

        return (int)Math.Clamp(Math.Floor(amount), int.MinValue, int.MaxValue);
    }
}
