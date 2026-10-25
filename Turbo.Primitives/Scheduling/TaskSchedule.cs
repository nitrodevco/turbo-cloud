using System;
using System.Globalization;

namespace Turbo.Primitives.Scheduling;

/// <summary>
/// When a scheduled task runs: a cron expression, optionally followed by a time zone
/// (<c>0 18 * * MON Europe/London</c>), or an interval (<c>00:05:00</c>). See
/// <c>docs/plugin-scheduler.md</c>.
/// </summary>
public sealed class TaskSchedule
{
    /// <summary>The shortest interval a task may run at: the scheduler looks once a second.</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(1);

    private TaskSchedule(string text, CronExpression? cron, TimeSpan? interval, bool zoneGiven)
    {
        Text = text;
        Cron = cron;
        Interval = interval;
        ZoneGiven = zoneGiven;
    }

    /// <summary>The schedule as written.</summary>
    public string Text { get; }

    public CronExpression? Cron { get; }

    public TimeSpan? Interval { get; }

    private bool ZoneGiven { get; }

    /// <summary>Reads <paramref name="text"/>; throws <see cref="FormatException"/> saying what is wrong.</summary>
    public static TaskSchedule Parse(string text)
    {
        var trimmed = text.Trim();

        if (
            trimmed.Contains(':', StringComparison.Ordinal)
            && !trimmed.Contains(' ', StringComparison.Ordinal)
            && TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out var interval)
        )
            return interval >= MinimumInterval
                ? new TaskSchedule(trimmed, null, interval, false)
                : throw new FormatException(
                    $"'{text}' is shorter than {MinimumInterval}, the shortest interval a task may run at."
                );

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var zoneAt =
            parts.Length is 6 || (parts.Length is 2 && parts[0].StartsWith('@'))
                ? parts.Length - 1
                : -1;
        TimeZoneInfo? zone = null;

        if (zoneAt >= 0)
        {
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(parts[zoneAt]);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                throw new FormatException(
                    $"'{parts[zoneAt]}' is not a time zone this server knows (use an id such as Europe/London).",
                    ex
                );
            }
        }

        var cron = CronExpression.Parse(
            string.Join(' ', zoneAt >= 0 ? parts[..zoneAt] : parts),
            zone
        );

        return new TaskSchedule(trimmed, cron, null, zone is not null);
    }

    /// <summary><see cref="Parse"/>, or null and why when <paramref name="text"/> is not a schedule.</summary>
    public static TaskSchedule? TryParse(string? text, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            return Parse(text);
        }
        catch (FormatException ex)
        {
            error = ex.Message;

            return null;
        }
    }

    /// <summary>The first run after <paramref name="after"/>; null when none comes.</summary>
    public DateTimeOffset? Next(DateTimeOffset after) =>
        Interval is { } every ? after + every : Cron!.Next(after);

    /// <summary>The schedule in words, as the admin panel lists it ("Mondays 18:00", "Every 5 minutes").</summary>
    public string Describe()
    {
        if (Interval is { } every)
            return DescribeInterval(every);

        var words = Cron!.Describe() ?? Cron.Text;

        return ZoneGiven ? $"{words} ({Cron.Zone.Id})" : words;
    }

    private static string DescribeInterval(TimeSpan every)
    {
        static string Count(double n, string unit) =>
            n == 1 ? $"Every {unit}" : $"Every {n.ToString(CultureInfo.InvariantCulture)} {unit}s";

        return every switch
        {
            { Ticks: var t } when t % TimeSpan.TicksPerDay == 0 => Count(every.TotalDays, "day"),
            { Ticks: var t } when t % TimeSpan.TicksPerHour == 0 => Count(every.TotalHours, "hour"),
            { Ticks: var t } when t % TimeSpan.TicksPerMinute == 0 => Count(
                every.TotalMinutes,
                "minute"
            ),
            { Ticks: var t } when t % TimeSpan.TicksPerSecond == 0 => Count(
                every.TotalSeconds,
                "second"
            ),
            _ => $"Every {every.ToString("c", CultureInfo.InvariantCulture)}",
        };
    }
}
