using FluentAssertions;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Players;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel acts on players through the hotel's own operator commands: each action becomes the
/// line that does it in game, written only from fields that cannot turn it into another command
/// or aim it at someone else.
/// </summary>
public sealed class PlayerActionLineTests
{
    [Theory]
    [InlineData("ban", "7d", "spamming the cafe", "ban alice 7d spamming the cafe")]
    [InlineData("ban", "perm", null, "ban alice perm")]
    [InlineData("silence", "30m", null, "silence alice 30m")]
    [InlineData("tradelock", "2w", null, "tradelock alice 2w")]
    [InlineData("unban", null, null, "unban alice")]
    [InlineData("unsilence", null, null, "unsilence alice")]
    [InlineData("untradelock", null, null, "untradelock alice")]
    [InlineData("disconnect", null, null, "disconnect alice")]
    [InlineData("warn", null, "Please stop.", "warn alice Please stop.")]
    [InlineData("alert", null, "Hello", "alert alice Hello")]
    public void EachAction_IsTheCommandThatDoesIt(
        string action,
        string? duration,
        string? text,
        string line
    )
    {
        PlayerActionLine.Build("alice", Request(action, duration, text)).Line.Should().Be(line);
    }

    [Fact]
    public void Give_NamesTheCurrencyAndAmount_AndANegativeAmountTakes()
    {
        PlayerActionLine
            .Build("alice", new PlayerActionRequest("give", null, null, "Credits", 500))
            .Line.Should()
            .Be("give alice credits 500");
        PlayerActionLine
            .Build("alice", new PlayerActionRequest("give", null, null, "duckets", -20))
            .Line.Should()
            .Be("give alice duckets -20");
        PlayerActionLine
            .Build("alice", new PlayerActionRequest("give", null, null, "credits", 0))
            .Line.Should()
            .BeNull();
    }

    [Fact]
    public void AReason_IsOneLine_SoItCannotStartAnotherCommand()
    {
        var line = PlayerActionLine.Build(
            "alice",
            Request("ban", "1d", "spam\nban everyone @online perm")
        );

        line.Line.Should().Be("ban alice 1d spam ban everyone @online perm");
        line.Line.Should().NotContain("\n");
    }

    [Fact]
    public void ACurrency_IsOneWord_SoItCannotAddArguments()
    {
        PlayerActionLine
            .Build("alice", new PlayerActionRequest("give", null, null, "credits @online", 5))
            .Line.Should()
            .BeNull();
    }

    [Theory]
    [InlineData("@room")]
    [InlineData("two words")]
    public void ANameACommandWouldMisread_IsLeftToTheConsole(string name)
    {
        // @room would ban everyone in the room; a space would end the name early.
        var (line, error) = PlayerActionLine.Build(name, Request("ban", "1d", null));

        line.Should().BeNull();
        error.Should().Contain("console");
    }

    [Theory]
    [InlineData("soon")]
    [InlineData("10")]
    [InlineData(null)]
    public void ADurationTheCommandsCannotRead_IsRefused(string? duration)
    {
        PlayerActionLine.Build("alice", Request("silence", duration, null)).Line.Should().BeNull();
    }

    [Fact]
    public void AnActionThePanelDoesNotOffer_IsRefused()
    {
        PlayerActionLine.Build("alice", Request("shutdown", null, null)).Line.Should().BeNull();
    }

    [Fact]
    public void AMessage_IsRequired()
    {
        PlayerActionLine.Build("alice", Request("warn", null, "   ")).Line.Should().BeNull();
    }

    private static PlayerActionRequest Request(string action, string? duration, string? text) =>
        new(action, duration, text, null, null);
}
