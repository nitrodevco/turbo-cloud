using FluentAssertions;
using Turbo.Primitives.Rooms.Object;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Habbo writes the heights in avatar statuses with a decimal (hhus, 2026-10-12: "mv 5,5,1.0",
/// "mv 7,7,0.0", "sit 1.0"); Turbo wrote "mv 5,5,1" and "sit 1".
/// </summary>
public sealed class AvatarStatusHeightFormatTests
{
    [Theory]
    [InlineData(1.0, "1.0")]
    [InlineData(0.0, "0.0")]
    [InlineData(0.5, "0.5")]
    [InlineData(1.25, "1.25")]
    public void Status_heights_always_carry_a_decimal(double value, string expected) =>
        Altitude.FromValue(value).ToStatusString().Should().Be(expected);
}
