using FluentAssertions;
using Turbo.Primitives.Scheduling;
using Xunit;

namespace Turbo.Tests.Plugins.Scheduling;

/// <summary>
/// When a scheduled task's schedule says it runs next, and what the panel says it is. Cron as cron
/// reads it (fields, names, the either-day rule); the clock changes as docs/plugin-scheduler.md
/// promises, on Europe/London's 2026 changes (29 March 01:00 UTC forward, 25 October 01:00 UTC back).
/// </summary>
public sealed class TaskScheduleTests
{
    private static DateTimeOffset Utc(string iso) =>
        DateTimeOffset.Parse(iso, null, System.Globalization.DateTimeStyles.AssumeUniversal);

    private static IEnumerable<DateTimeOffset> Runs(string schedule, string after, int count)
    {
        var parsed = TaskSchedule.Parse(schedule);
        var at = Utc(after);

        for (var i = 0; i < count; i++)
        {
            at = parsed.Next(at) ?? throw new InvalidOperationException("no next run");

            yield return at;
        }
    }

    [Theory]
    [InlineData(
        "0 18 * * MON UTC",
        "2026-10-25T12:00:00Z",
        "2026-10-26T18:00:00Z",
        "2026-11-02T18:00:00Z"
    )]
    // Both day fields given: either matches (the 13th or a Friday).
    [InlineData(
        "0 0 13 * FRI UTC",
        "2026-10-01T00:00:00Z",
        "2026-10-02T00:00:00Z",
        "2026-10-09T00:00:00Z"
    )]
    [InlineData(
        "@weekly UTC",
        "2026-10-21T10:00:00Z",
        "2026-10-25T00:00:00Z",
        "2026-11-01T00:00:00Z"
    )]
    [InlineData(
        "*/20 9-10 1,15 JAN,jul * UTC",
        "2026-06-30T23:00:00Z",
        "2026-07-01T09:00:00Z",
        "2026-07-01T09:20:00Z"
    )]
    [InlineData("00:05:00", "2026-10-25T12:00:00Z", "2026-10-25T12:05:00Z", "2026-10-25T12:10:00Z")]
    public void The_next_runs_are_the_ones_the_schedule_names(
        string schedule,
        string after,
        string first,
        string second
    ) => Runs(schedule, after, 2).Should().Equal(Utc(first), Utc(second));

    [Fact]
    public void A_time_the_clocks_skip_runs_once_as_they_go_forward()
    {
        // 01:30 does not exist on 29 March: it runs at 02:00 BST (01:00 UTC), then at 01:30 BST again.
        Runs("30 1 * * * Europe/London", "2026-03-28T12:00:00Z", 3)
            .Should()
            .Equal(
                Utc("2026-03-29T01:00:00Z"),
                Utc("2026-03-30T00:30:00Z"),
                Utc("2026-03-31T00:30:00Z")
            );
    }

    [Fact]
    public void A_time_that_comes_twice_as_the_clocks_go_back_runs_once()
    {
        // 01:30 comes at 00:30 UTC (BST) and at 01:30 UTC (GMT) on 25 October: only the first.
        Runs("30 1 * * * Europe/London", "2026-10-24T12:00:00Z", 2)
            .Should()
            .Equal(Utc("2026-10-25T00:30:00Z"), Utc("2026-10-26T01:30:00Z"));
    }

    [Fact]
    public void An_every_hour_schedule_keeps_its_pace_through_both_clock_changes()
    {
        Runs("*/30 * * * * Europe/London", "2026-10-25T00:15:00Z", 4)
            .Should()
            .Equal(
                Utc("2026-10-25T00:30:00Z"),
                Utc("2026-10-25T01:00:00Z"),
                Utc("2026-10-25T01:30:00Z"),
                Utc("2026-10-25T02:00:00Z")
            );

        // Forward: 01:00 and 01:30 local do not exist, and nothing is made up for them.
        Runs("*/30 * * * * Europe/London", "2026-03-29T00:15:00Z", 3)
            .Should()
            .Equal(
                Utc("2026-03-29T00:30:00Z"),
                Utc("2026-03-29T01:00:00Z"),
                Utc("2026-03-29T01:30:00Z")
            );
    }

    [Fact]
    public void A_date_that_never_comes_has_no_next_run() =>
        TaskSchedule.Parse("0 0 31 2 *").Next(Utc("2026-01-01T00:00:00Z")).Should().BeNull();

    [Theory]
    [InlineData("0 18 * * MON", "Mondays 18:00")]
    [InlineData("0 18 * * 1,5", "Mondays and Fridays 18:00")]
    [InlineData("30 3 * * *", "Every day 03:30")]
    [InlineData("0 9 * * 1-5", "Weekdays 09:00")]
    [InlineData("15 * * * *", "Every hour at :15")]
    [InlineData("0 0 1 * *", "Day 1 of the month 00:00")]
    [InlineData("0 12 * 12 SUN", "Sundays 12:00 in December")]
    [InlineData("0 18 * * MON Europe/London", "Mondays 18:00 (Europe/London)")]
    [InlineData("*/7 2-4 * * *", "*/7 2-4 * * *")]
    [InlineData("00:05:00", "Every 5 minutes")]
    [InlineData("01:00:00", "Every hour")]
    [InlineData("7.00:00:00", "Every 7 days")]
    public void The_panel_says_the_schedule_in_words(string schedule, string words) =>
        TaskSchedule.Parse(schedule).Describe().Should().Be(words);

    [Fact]
    public void A_schedule_that_cant_be_read_is_refused_with_the_reason()
    {
        var refused = new[]
        {
            "60 * * * *",
            "0 25 * * *",
            "0 0 0 * *",
            "0 0 * * FOO",
            "5-1 * * * *",
            "* * *",
            "00:00:00.5",
            "0 0 * * * Not/AZone",
        };

        refused
            .Where(text => TaskSchedule.TryParse(text, out var error) is not null || error is null)
            .Should()
            .BeEmpty("each of these is not a schedule and says why");
    }
}
