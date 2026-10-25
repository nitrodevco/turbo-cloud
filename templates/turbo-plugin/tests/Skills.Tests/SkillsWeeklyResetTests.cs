using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Testing;
using Xunit;

namespace Skills.Tests;

/// <summary>
/// The weekly job against <see cref="FakeHotel"/>'s clock: when the server runs it next, and what
/// one run does (turbo-cloud <c>docs/plugin-scheduler.md</c>).
/// </summary>
public sealed class SkillsWeeklyResetTests
{
    // Saturday noon UTC: still the weekend in every time zone. The job's times are the server's own.
    private static readonly DateTimeOffset Saturday = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTime MondayMidnight = new(2026, 10, 26, 0, 0, 0);

    private readonly FakeHotel _hotel = new(Saturday);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task It_runs_next_on_monday_at_midnight_and_puts_everyone_back_to_level_one()
    {
        var book = new SkillBook(new Settings(), _hotel);
        var job = new SkillsWeeklyReset(book);
        var alice = _hotel.Players.Add("Alice");

        await book.TrainAsync(alice, Ct);
        await book.TrainAsync(alice, Ct);

        var due = new DateTimeOffset(
            MondayMidnight,
            TimeZoneInfo.Local.GetUtcOffset(MondayMidnight)
        );

        Assert.Equal(due, _hotel.NextRunOf(job));

        _hotel.Clock.Set(due);
        await _hotel.RunAsync(job, ct: Ct);

        Assert.Equal(new Skill(1, 0, 100), book.Of(alice));
    }

    private sealed class Settings : IOptionsMonitor<SkillsSettings>
    {
        public SkillsSettings CurrentValue { get; } = new();

        public SkillsSettings Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<SkillsSettings, string?> listener) => null;
    }
}
