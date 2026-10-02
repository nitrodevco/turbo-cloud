using FluentAssertions;
using Turbo.Primitives.Commands;
using Xunit;

namespace Turbo.Tests.Commands;

public class CommandDurationTests
{
    [Theory]
    [InlineData("30m", 30)]
    [InlineData("12H", 12 * 60)]
    [InlineData("7d", 7 * 24 * 60)]
    [InlineData("2w", 14 * 24 * 60)]
    [InlineData(" 90m ", 90)]
    public void ANumberAndAUnit_IsThatLong(string text, int minutes)
    {
        CommandDuration.TryParse(text, out var duration).Should().BeTrue();

        duration.Span.Should().Be(TimeSpan.FromMinutes(minutes));
        duration.IsPermanent.Should().BeFalse();
    }

    [Theory]
    [InlineData("perm")]
    [InlineData("PERMANENT")]
    public void Perm_HasNoEnd(string text)
    {
        CommandDuration.TryParse(text, out var duration).Should().BeTrue();

        duration.IsPermanent.Should().BeTrue();
        duration.EndsAt(DateTime.UtcNow).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("7")] // minutes or days? a bare number is how a one hour ban becomes a year
    [InlineData("d")]
    [InlineData("0d")]
    [InlineData("-3d")]
    [InlineData("1.5h")]
    [InlineData("3x")]
    [InlineData("forever")]
    [InlineData("99999d")] // over ten years
    [InlineData("2147483647w")] // parses as an int, but cannot be made into a TimeSpan
    [InlineData("99999999999d")] // too big to be a number at all
    public void ASpanThatIsNotOne_IsRefused(string text) =>
        CommandDuration.TryParse(text, out _).Should().BeFalse();

    [Fact]
    public void EndsAt_AddsTheSpanToWhenItStarts()
    {
        var start = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        CommandDuration.TryParse("2h", out var duration);

        duration.EndsAt(start).Should().Be(start.AddHours(2));
    }

    [Theory]
    [InlineData("45m", "45 min")]
    [InlineData("3h", "3 h")]
    [InlineData("7d", "7 d")]
    [InlineData("2w", "14 d")]
    [InlineData("perm", "permanent")]
    public void ItReadsBackInTheUnitThatFits(string typed, string shown)
    {
        CommandDuration.TryParse(typed, out var duration);

        duration.ToString().Should().Be(shown);
    }
}
