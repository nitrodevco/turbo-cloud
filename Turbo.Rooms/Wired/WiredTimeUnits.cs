using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>Duration unit conversion for the variable age and time utility boxes.</summary>
public static class WiredTimeUnits
{
    private const long SECOND = 1000;
    private const long MINUTE = 60 * SECOND;
    private const long HOUR = 60 * MINUTE;
    private const long DAY = 24 * HOUR;
    private const long WEEK = 7 * DAY;
    private const long MONTH = 30 * DAY;
    private const long YEAR = 365 * DAY;

    public static long ToMilliseconds(long amount, WiredTimeUnitType unit) =>
        amount
        * unit switch
        {
            WiredTimeUnitType.Milliseconds => 1,
            WiredTimeUnitType.Seconds => SECOND,
            WiredTimeUnitType.Minutes => MINUTE,
            WiredTimeUnitType.Hours => HOUR,
            WiredTimeUnitType.Days => DAY,
            WiredTimeUnitType.Weeks => WEEK,
            WiredTimeUnitType.Months => MONTH,
            WiredTimeUnitType.Years => YEAR,
            _ => SECOND,
        };
}
