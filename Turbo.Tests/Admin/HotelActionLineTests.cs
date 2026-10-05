using FluentAssertions;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Commands;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The dashboard acts on the whole hotel through its own operator commands: each action is the
/// line that does it in game, written only from fields that cannot turn it into another command.
/// </summary>
public sealed class HotelActionLineTests
{
    [Theory]
    [InlineData("alert", null, "Back in five", "hotelalert Back in five")]
    [InlineData("maintenance", 10, "Updating furni", "maintenance 10 Updating furni")]
    [InlineData("maintenance", 0, null, "maintenance 0")]
    [InlineData("maintenance-off", null, null, "maintenance off")]
    [InlineData("shutdown", 5, "Restarting", "shutdown 5 Restarting")]
    [InlineData("shutdown-cancel", null, null, "shutdown cancel")]
    public void EachAction_IsTheCommandThatDoesIt(
        string action,
        int? minutes,
        string? message,
        string line
    )
    {
        HotelActionLine
            .Build(new HotelActionRequest(action, minutes, message))
            .Line.Should()
            .Be(line);
    }

    [Fact]
    public void AMessage_IsOneLine_SoItCannotStartAnotherCommand()
    {
        HotelActionLine
            .Build(new HotelActionRequest("alert", null, "Hello\r\nshutdown 0"))
            .Line.Should()
            .Be("hotelalert Hello shutdown 0");
    }

    [Theory]
    [InlineData("maintenance", null)]
    [InlineData("maintenance", -1)]
    [InlineData("shutdown", null)]
    public void ACountdown_NeedsMinutes(string action, int? minutes)
    {
        HotelActionLine.Build(new HotelActionRequest(action, minutes, null)).Line.Should().BeNull();
    }

    [Fact]
    public void AnAlert_NeedsAMessage_AndAnUnknownActionIsRefused()
    {
        HotelActionLine.Build(new HotelActionRequest("alert", null, " ")).Line.Should().BeNull();
        HotelActionLine.Build(new HotelActionRequest("reload", null, null)).Line.Should().BeNull();
    }
}
