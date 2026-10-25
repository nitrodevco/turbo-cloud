using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True when the calendar date matches. Eight params: use-day flag, use-year flag, weekday
/// bitmask (bit 0 = Monday), day min, day max, month bitmask (bit 0 = January), year min,
/// year max. An empty bitmask means every weekday or month. The string param is a timezone.
/// </summary>
[RoomObjectLogic("wf_cnd_match_date")]
public class WiredConditionDateMatches(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.DATE_MATCHES;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false), // use day range
            new WiredBoolParamRule(false), // use year range
            new WiredRangeParamRule(0, 127, 127), // weekday mask
            new WiredRangeParamRule(1, 31, 1),
            new WiredRangeParamRule(1, 31, 1),
            new WiredRangeParamRule(0, 4095, 4095), // month mask
            new WiredRangeParamRule(0, 9999, 0),
            new WiredRangeParamRule(0, 9999, 0),
        ];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var now = WiredTimeZones.GetLocalTime(_roomGrain, _wiredData.StringParam);

        var weekdayMask = GetIntParamOrDefault(2, 0);

        if (weekdayMask != 0)
        {
            var mondayBased = ((int)now.DayOfWeek + 6) % 7;

            if ((weekdayMask & (1 << mondayBased)) == 0)
                return false;
        }

        var monthMask = GetIntParamOrDefault(5, 0);

        if (monthMask != 0 && (monthMask & (1 << (now.Month - 1))) == 0)
            return false;

        // A day range whose min is past its max wraps over the month end (30..26 held on the
        // 25th, 26..24 did not); a year range like that never holds (2027..2025 failed in 2026)
        // (official client capture 2026-10-25, wired matrix condition.29.B2b / B2c, 5/5 each).
        if (GetIntParamOrDefault(0, false))
        {
            var min = GetIntParamOrDefault(3, 1);
            var max = GetIntParamOrDefault(4, 31);
            var inRange =
                min <= max ? now.Day >= min && now.Day <= max : now.Day >= min || now.Day <= max;

            if (!inRange)
                return false;
        }

        if (GetIntParamOrDefault(1, false))
        {
            var min = GetIntParamOrDefault(6, 0);
            var max = GetIntParamOrDefault(7, 9999);

            if (now.Year < min || now.Year > max)
                return false;
        }

        return true;
    }
}
