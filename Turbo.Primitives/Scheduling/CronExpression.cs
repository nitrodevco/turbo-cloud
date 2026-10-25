using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Turbo.Primitives.Scheduling;

/// <summary>
/// A five-field cron expression (minute hour day-of-month month day-of-week) in a time zone, as
/// cron reads one: <c>*</c>, lists, ranges and steps, month and weekday names, 0 or 7 for Sunday,
/// and a day that matches when either day field does if both are given. Runs are whole minutes.
/// Clocks going forward: a time they skip runs once as they do (at the first minute after the gap).
/// Clocks going back: a time that comes twice runs once, the first time, unless the hour field is
/// <c>*</c> (then it runs both times, so a job every 30 minutes keeps its pace).
/// </summary>
public sealed class CronExpression
{
    private static readonly string[] MONTHS =
    [
        "JAN",
        "FEB",
        "MAR",
        "APR",
        "MAY",
        "JUN",
        "JUL",
        "AUG",
        "SEP",
        "OCT",
        "NOV",
        "DEC",
    ];

    private static readonly string[] DAYS = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

    /// <summary>How far ahead a next run is looked for: a leap day, at worst, comes every 8 years.</summary>
    private const int SEARCH_DAYS = 366 * 8 + 2;

    private readonly bool[] _minutes;
    private readonly bool[] _hours;
    private readonly bool[] _daysOfMonth;
    private readonly bool[] _months;
    private readonly bool[] _daysOfWeek;
    private readonly bool _anyDayOfMonth;
    private readonly bool _anyDayOfWeek;

    private CronExpression(string text, string[] fields, TimeZoneInfo zone)
    {
        Text = text;
        Zone = zone;
        _minutes = Field(fields[0], 0, 59, null, "minute");
        _hours = Field(fields[1], 0, 23, null, "hour");
        _daysOfMonth = Field(fields[2], 1, 31, null, "day of month");
        _months = Field(fields[3], 1, 12, MONTHS, "month");

        var days = Field(fields[4], 0, 7, DAYS, "day of week");

        days[0] |= days[7];
        _daysOfWeek = days[..7];
        _anyDayOfMonth = fields[2].StartsWith('*');
        _anyDayOfWeek = fields[4].StartsWith('*');
    }

    /// <summary>The expression as written (its five fields, or the <c>@</c> name).</summary>
    public string Text { get; }

    public TimeZoneInfo Zone { get; }

    /// <summary>Whether every hour matches, which makes a repeated hour run twice.</summary>
    internal bool EveryHour => _hours.All(x => x);

    /// <summary>
    /// Reads <paramref name="text"/>: five fields or <c>@hourly</c>, <c>@daily</c>
    /// (<c>@midnight</c>), <c>@weekly</c>, <c>@monthly</c>, <c>@yearly</c> (<c>@annually</c>).
    /// Throws <see cref="FormatException"/> naming the field that is wrong.
    /// </summary>
    public static CronExpression Parse(string text, TimeZoneInfo? zone = null)
    {
        var trimmed = text.Trim();
        var fields = trimmed.ToLowerInvariant() switch
        {
            "@hourly" => "0 * * * *",
            "@daily" or "@midnight" => "0 0 * * *",
            "@weekly" => "0 0 * * 0",
            "@monthly" => "0 0 1 * *",
            "@yearly" or "@annually" => "0 0 1 1 *",
            _ => trimmed,
        };
        var parts = fields.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 5)
            throw new FormatException(
                $"'{text}' is not a cron expression: it needs five fields (minute hour day month weekday), such as 0 18 * * MON."
            );

        return new CronExpression(trimmed, parts, zone ?? TimeZoneInfo.Local);
    }

    /// <summary>The first run after <paramref name="after"/>; null when none comes (31 February).</summary>
    public DateTimeOffset? Next(DateTimeOffset after)
    {
        var day = TimeZoneInfo.ConvertTime(after, Zone).Date;
        var candidates = new List<DateTimeOffset>();

        for (var i = 0; i < SEARCH_DAYS; i++, day = day.AddDays(1))
        {
            if (!DayMatches(day))
                continue;

            candidates.Clear();

            for (var h = 0; h < 24; h++)
            {
                if (!_hours[h])
                    continue;

                for (var m = 0; m < 60; m++)
                    if (_minutes[m])
                        AddInstants(day.AddHours(h).AddMinutes(m), candidates);
            }

            DateTimeOffset? first = null;

            foreach (var instant in candidates)
                if (instant > after && (first is null || instant < first))
                    first = instant;

            if (first is not null)
                return first;
        }

        return null;
    }

    /// <summary>
    /// What the expression says in words ("Mondays 18:00", "Every day 03:30", "Every hour at :15"),
    /// or null when it is too irregular to say shortly.
    /// </summary>
    public string? Describe()
    {
        var minutes = Indexes(_minutes);
        var hours = Indexes(_hours);
        var allMonths = _months.Skip(1).All(x => x);
        var time = (minutes.Length, hours.Length) switch
        {
            (1, 1) => $"{hours[0]:00}:{minutes[0]:00}",
            (1, 24) => $"every hour at :{minutes[0]:00}",
            (60, 24) => "every minute",
            _ => null,
        };

        if (time is null)
            return null;

        string? days;

        if (_anyDayOfMonth && _anyDayOfWeek)
            days = null;
        else if (_anyDayOfMonth)
            days = Weekdays(Indexes(_daysOfWeek));
        else if (_anyDayOfWeek)
            days =
                Indexes(_daysOfMonth) is var dom && dom.Length == 1
                    ? $"Day {dom[0]} of the month"
                    : $"Days {Join(dom.Select(d => d.ToString(CultureInfo.InvariantCulture)))} of the month";
        else
            return null;

        var text = days is null
            ? (
                time.StartsWith("every", StringComparison.Ordinal)
                    ? Capital(time)
                    : $"Every day {time}"
            )
            : $"{days}{(time.StartsWith("every", StringComparison.Ordinal) ? ", " : " ")}{time}";

        if (!allMonths)
            text +=
                " in "
                + Join(
                    Indexes(_months)
                        .Select(m => CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m))
                );

        return text;
    }

    private bool DayMatches(DateTime day)
    {
        if (!_months[day.Month])
            return false;

        var dom = _daysOfMonth[day.Day];
        var dow = _daysOfWeek[(int)day.DayOfWeek];

        return _anyDayOfMonth || _anyDayOfWeek ? dom && dow : dom || dow;
    }

    private void AddInstants(DateTime local, List<DateTimeOffset> into)
    {
        if (Zone.IsInvalidTime(local))
        {
            if (EveryHour)
                return;

            // Skipped by the clocks going forward: once, at the first minute that exists.
            var after = local;

            while (Zone.IsInvalidTime(after))
                after = after.AddMinutes(1);

            into.Add(new DateTimeOffset(after, Zone.GetUtcOffset(after)));

            return;
        }

        if (Zone.IsAmbiguousTime(local))
        {
            // The larger offset is the first time the clock reads it.
            var offsets = Zone.GetAmbiguousTimeOffsets(local).OrderDescending().ToArray();

            foreach (var offset in EveryHour ? offsets : offsets[..1])
                into.Add(new DateTimeOffset(local, offset));

            return;
        }

        into.Add(new DateTimeOffset(local, Zone.GetUtcOffset(local)));
    }

    private static bool[] Field(string text, int min, int max, string[]? names, string field)
    {
        var set = new bool[max + 1];

        foreach (var item in text.Split(','))
        {
            var (range, step) = item.Split('/') switch
            {
                [var r] => (r, 1),
                [var r, var s]
                    when int.TryParse(s, CultureInfo.InvariantCulture, out var n) && n > 0 => (
                    r,
                    n
                ),
                _ => throw Bad(text, field, min, max),
            };
            int from,
                to;

            if (range == "*")
                (from, to) = (min, max);
            else if (range.Split('-') is [var a, var b])
                (from, to) = (
                    Value(a, names, text, field, min, max),
                    Value(b, names, text, field, min, max)
                );
            else
            {
                from = Value(range, names, text, field, min, max);
                to = step > 1 ? max : from;
            }

            if (from > to)
                throw Bad(text, field, min, max);

            for (var v = from; v <= to; v += step)
                set[v] = true;
        }

        return set;
    }

    private static int Value(
        string text,
        string[]? names,
        string whole,
        string field,
        int min,
        int max
    )
    {
        if (int.TryParse(text, CultureInfo.InvariantCulture, out var n))
            return n >= min && n <= max ? n : throw Bad(whole, field, min, max);

        var index = names is null ? -1 : Array.IndexOf(names, text.ToUpperInvariant());

        // Month names count from 1, weekday names from 0 (Sunday).
        return index < 0 ? throw Bad(whole, field, min, max)
            : names == MONTHS ? index + 1
            : index;
    }

    private static FormatException Bad(string text, string field, int min, int max) =>
        new(
            $"'{text}' is not a {field} cron reads: use {min}-{max}, *, a list (1,15), a range (1-5) or a step (*/10)."
        );

    private static int[] Indexes(bool[] set) =>
        [.. set.Select((on, i) => (on, i)).Where(x => x.on).Select(x => x.i)];

    private static string Weekdays(int[] days)
    {
        if (days.SequenceEqual([1, 2, 3, 4, 5]))
            return "Weekdays";

        if (days.SequenceEqual([0, 6]))
            return "Weekends";

        // Monday first, as a week reads.
        return Join(
            days.OrderBy(d => (d + 6) % 7)
                .Select(d =>
                    CultureInfo.InvariantCulture.DateTimeFormat.GetDayName((DayOfWeek)d) + "s"
                )
        );
    }

    private static string Join(IEnumerable<string> items)
    {
        var list = items.ToList();

        return list.Count <= 1
            ? string.Concat(list)
            : $"{string.Join(", ", list[..^1])} and {list[^1]}";
    }

    private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
