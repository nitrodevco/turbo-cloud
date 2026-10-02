using FluentAssertions;
using Turbo.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>
/// A line that reaches a lot of the hotel waits for <c>:confirm</c>, as Terraform's plan waits for
/// apply: the executor is told what it would do, and nothing happens until they say so.
/// </summary>
public class ConfirmationTests
{
    private readonly OperatorFixture _hotel = new();

    public ConfirmationTests()
    {
        // The fixture keeps the defaults: ten players or more waits, for 30 seconds.
        for (var i = 0; i < 12; i++)
            _hotel.WithPlayer(100 + i, $"p{i:00}");

        _hotel.Commands.Register([
            new ConfirmCommand(),
            new OperatorMassCommand(),
            new OperatorProbeCommand(),
        ]);
    }

    private static FakeExecutor Staff(int roomId = 7) =>
        new(
            2,
            "staff",
            roomId,
            [2, 100, 101],
            "command.opmass",
            OperatorMassCommand.MASS_NODE,
            "command.opprobe",
            PermissionNodes.Command.CONFIRM
        );

    [Fact]
    public async Task ASelectorReachingManyPlayers_WaitsAndSaysWhatItWouldDo()
    {
        var staff = Staff();

        var outcome = await _hotel.RunAsync("opmass", staff, "@online");

        outcome.Should().Be(CommandOutcome.AwaitingConfirmation);
        staff
            .Replies.Should()
            .Equal("That reaches 12 players. Type :confirm within 30 seconds to go ahead.");
    }

    [Fact]
    public async Task Confirm_RunsTheLine_AsItWasTyped()
    {
        var staff = Staff();

        await _hotel.RunAsync("opmass", staff, "@online");
        var outcome = await _hotel.RunAsync("confirm", staff, "");

        outcome.Should().Be(CommandOutcome.Completed);
        staff.Replies.Should().HaveCount(2);
        staff.Replies[1].Should().StartWith("Reached p00,p01,");
    }

    [Fact]
    public async Task ASelectorReachingFewPlayers_RunsAtOnce()
    {
        var staff = Staff();

        await _hotel.RunAsync("opmass", staff, "@room");

        staff.Replies.Should().ContainSingle().Which.Should().StartWith("Reached ");
    }

    [Fact]
    public async Task Confirm_OnlineSelector_KeepsTheApprovedPlayers_WhenPresenceChanges()
    {
        var staff = Staff();
        await _hotel.RunAsync("opmass", staff, "@online");

        _hotel.Online.Remove(100);
        _hotel.WithPlayer(200, "newcomer");
        await _hotel.RunAsync("confirm", staff, "");

        staff.Replies[^1].Should().Contain("p00").And.NotContain("newcomer");
    }

    [Fact]
    public async Task Confirm_RoomSelector_KeepsTheApprovedPlayers_InTheSameRoom()
    {
        var players = Enumerable.Range(100, 12).ToArray();
        var staff = new FakeExecutor(
            2,
            "staff",
            7,
            players,
            "command.opmass",
            OperatorMassCommand.MASS_NODE
        );
        await _hotel.RunAsync("opmass", staff, "@room");

        var now = Staff();
        await _hotel.RunAsync("confirm", now, "");

        now.Replies.Should().ContainSingle().Which.Should().Contain("p11");
        staff.Replies.Should().ContainSingle();
    }

    [Fact]
    public async Task Confirm_RechecksTheCurrentExecutorsPermissions()
    {
        await _hotel.RunAsync("opmass", Staff(), "@online");
        var demoted = new FakeExecutor(2, "staff", 7, [2], PermissionNodes.Command.CONFIRM);

        var outcome = await _hotel.RunAsync("confirm", demoted, "");

        outcome.Should().Be(CommandOutcome.Failed);
        demoted.Replies.Should().Equal("You can't use that command.");
    }

    [Fact]
    public async Task Confirm_RechecksTheSelectorPermission()
    {
        var staff = Staff();
        await _hotel.RunAsync("opmass", staff, "@online");
        staff.Revoke(OperatorMassCommand.MASS_NODE);

        var outcome = await _hotel.RunAsync("confirm", staff, "");

        outcome.Should().Be(CommandOutcome.Failed);
        staff.Replies[^1].Should().Be("You can't use @online with that command.");
    }

    [Fact]
    public async Task Confirm_DoesNotRunAReplacementCommandWithTheSameName()
    {
        using var registration = _hotel.Commands.Register([new ConfirmationReplacementCommand()]);
        var console = new FakeExecutor(null, "console");
        await _hotel.RunAsync("replacementprobe", console, "");
        registration.Dispose();
        _hotel.Commands.Register([new ConfirmationReplacementCommand()]);

        var outcome = await _hotel.RunAsync("confirm", console, "");

        outcome.Should().Be(CommandOutcome.Failed);
        console.Replies[^1].Should().Be("There is nothing to confirm.");
    }

    [Fact]
    public async Task Confirm_UsesTheOriginalBoundArguments()
    {
        var command = new ConfirmationReplacementCommand();
        _hotel.Commands.Register([command]);
        var console = new FakeExecutor(null, "console");

        await _hotel.RunAsync("replacementprobe", console, "original text");
        await _hotel.RunAsync("confirm", console, "");

        command.Calls.Should().HaveCount(2);
        command.Calls[1].Should().BeSameAs(command.Calls[0]);
        command.Calls[1].Text?.Text.Should().Be("original text");
    }

    [Fact]
    public async Task Confirm_WithNothingWaiting_SaysSo()
    {
        var staff = Staff();

        await _hotel.RunAsync("confirm", staff, "");

        staff.Replies.Should().Equal("There is nothing to confirm.");
    }

    [Fact]
    public async Task Confirm_CannotIntroduceAnAudienceThatWasNotApproved()
    {
        var command = new ConfirmationReplacementCommand { AddAudienceAfterConfirmation = true };
        _hotel.Commands.Register([command]);
        var console = new FakeExecutor(null, "console");
        await _hotel.RunAsync("replacementprobe", console, "");

        var outcome = await _hotel.RunAsync("confirm", console, "");

        outcome.Should().Be(CommandOutcome.Failed);
        command.Calls.Should().ContainSingle();
        console.Replies[^1].Should().Be(CommandReplyKeys.Defaults[CommandReplyKeys.FAILED]);
    }

    [Fact]
    public async Task Confirm_IsSpentOnce()
    {
        var staff = Staff();

        await _hotel.RunAsync("opmass", staff, "@online");
        await _hotel.RunAsync("confirm", staff, "");
        await _hotel.RunAsync("confirm", staff, "");

        staff.Replies[^1].Should().Be("There is nothing to confirm.");
    }

    [Fact]
    public async Task Confirm_TooLate_HasNothingToRun()
    {
        var staff = Staff();

        await _hotel.RunAsync("opmass", staff, "@online");
        _hotel.Clock.Advance(TimeSpan.FromSeconds(31));
        await _hotel.RunAsync("confirm", staff, "");

        staff.Replies[^1].Should().Be("There is nothing to confirm.");
    }

    [Fact]
    public async Task Confirm_FromAnotherRoom_IsRefused_SoAtRoomMeansWhatWasShown()
    {
        await _hotel.RunAsync("opmass", Staff(roomId: 7), "@online");

        var elsewhere = Staff(roomId: 8);
        await _hotel.RunAsync("confirm", elsewhere, "");

        elsewhere.Replies.Should().Equal("That was typed in another room. Type it again here.");
    }

    [Fact]
    public async Task ANewLineToConfirm_ReplacesTheOneWaiting()
    {
        await _hotel.RunAsync("opmass", Staff(roomId: 7), "@online");

        var here = Staff(roomId: 8);
        await _hotel.RunAsync("opmass", here, "@online");
        await _hotel.RunAsync("confirm", here, "");

        // Only the line from room 8 was waiting, so room 8 may confirm it.
        here.Replies[^1].Should().StartWith("Reached ");
    }

    [Fact]
    public async Task Confirm_ChecksTheNodeAgain_WhichMayHaveBeenTakenAway()
    {
        var staff = Staff();
        await _hotel.RunAsync("opmass", staff, "@online");

        staff.Revoke("command.opmass");
        await _hotel.RunAsync("confirm", staff, "");

        staff.Replies[^1].Should().Be("You can't use that command.");
    }

    [Fact]
    public async Task TheConsole_ConfirmsToo()
    {
        var console = new FakeExecutor(null, "console");

        await _hotel.RunAsync("opmass", console, "@online");
        await _hotel.RunAsync("confirm", console, "");

        console.Replies.Should().HaveCount(2);
        console.Replies[1].Should().StartWith("Reached ");
    }

    [Fact]
    public async Task AnAwaitingLine_IsLoggedAsSuch_AndTheConfirmedRunAsCompleted()
    {
        var staff = Staff();

        await _hotel.RunAsync("opmass", staff, "@online");
        await _hotel.RunAsync("confirm", staff, "");

        await using var ctx = _hotel.Db.CreateDbContext();
        ctx.CommandLogs.Where(x => x.Command == "opmass")
            .Select(x => x.Outcome)
            .Should()
            .BeEquivalentTo(["confirm", "completed"]);
    }
}
