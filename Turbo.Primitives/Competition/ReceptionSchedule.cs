using System;
using System.Globalization;

namespace Turbo.Primitives.Competition;

/// <summary>
/// Resolves the reception's date/code schedules against server UTC time. The client supplies
/// semicolon-separated date,code entries and identifies replies by the original input string.
/// </summary>
public static class ReceptionSchedule
{
    public static string GetCurrentCode(string schedule, DateTimeOffset now)
    {
        var code = string.Empty;
        var latest = DateTimeOffset.MinValue;

        foreach (var entry in schedule.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = entry.IndexOf(',');

            if (separator < 0)
                continue;

            if (
                TryParseTime(entry[..separator], out var startsAt)
                && startsAt <= now
                && startsAt >= latest
            )
            {
                latest = startsAt;
                code = entry[(separator + 1)..].Trim();
            }
        }

        return code;
    }

    public static int GetSecondsUntil(string time, DateTimeOffset now)
    {
        if (!TryParseTime(time, out var target))
            return 0;

        return (int)Math.Clamp(Math.Ceiling((target - now).TotalSeconds), 0, int.MaxValue);
    }

    private static bool TryParseTime(string value, out DateTimeOffset time) =>
        DateTimeOffset.TryParseExact(
            value.Trim(),
            ["yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out time
        );
}
