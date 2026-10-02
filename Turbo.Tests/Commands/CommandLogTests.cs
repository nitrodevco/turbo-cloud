using FluentAssertions;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Commands.Events;
using Turbo.Primitives.Commands.Snapshots;
using Turbo.Primitives.Players.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

public class CommandLogTests
{
    private readonly CommandRoomFixture _room = new();
    private readonly ProbeCommand _probe = new();

    public CommandLogTests() => _room.Commands.Register([_probe]);

    private IReadOnlyCollection<CommandLogSnapshot> Pending =>
        (Queue<CommandLogSnapshot>)
            RoomHarness.GetMember(_room.Harness.State, "PendingCommandLogs")!;

    [Fact]
    public async Task AUseByAnExecutorHoldingCommandLog_IsQueuedWithWhatTheyTyped()
    {
        _room.AddPlayer(2, "command.probe", PermissionNodes.Command.LOG);

        await _room.SayAsync(2, ":probe   a very rude word  ");

        var logged = Pending.Should().ContainSingle().Which;
        logged.Command.Should().Be("probe");
        logged.Arguments.Should().Be("a very rude word");
        logged.PlayerId.Value.Should().Be(2);
        logged.Outcome.Should().Be(CommandOutcome.Completed);
        logged.LoggedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task AUseByAnyoneElse_IsNotLogged()
    {
        _room.AddPlayer(2, "command.probe");

        await _room.SayAsync(2, ":probe quiet");

        Pending.Should().BeEmpty();
    }

    [Fact]
    public async Task ACommandReturningFailure_IsLoggedAsFailed()
    {
        _probe.Result = CommandResult.Fail("greeted", "Alice");
        _room.AddPlayer(2, "command.probe", PermissionNodes.Command.LOG);

        await _room.SayAsync(2, ":probe");

        Pending.Should().ContainSingle().Which.Outcome.Should().Be(CommandOutcome.Failed);
    }

    [Fact]
    public async Task CompletionHooks_SeeTheRoomAuditAlreadyQueued()
    {
        _room.AddPlayer(2, "command.probe", PermissionNodes.Command.LOG);
        var audited = false;
        _room.Events.On<CommandExecutedEvent>(_ => audited = Pending.Count == 1);

        await _room.SayAsync(2, ":probe");

        audited.Should().BeTrue();
    }

    [Fact]
    public async Task ARefusedAttemptByALoggedExecutor_IsLoggedWithItsOutcome()
    {
        _room.AddPlayer(2, PermissionNodes.Command.LOG);

        await _room.SayAsync(2, ":probe sneaky");

        Pending.Should().ContainSingle().Which.Outcome.Should().Be(CommandOutcome.Refused);
    }

    [Fact]
    public async Task ALineThatIsNotACommand_IsNotLogged()
    {
        _room.AddPlayer(2, "command.probe", PermissionNodes.Command.LOG);

        await _room.SayAsync(2, "just chatting");

        Pending.Should().BeEmpty();
    }

    [Fact]
    public async Task AFloodedCommand_IsNotLogged()
    {
        _room.AddPlayer(2, "command.probe", PermissionNodes.Command.LOG);

        for (var i = 0; i < _room.FloodLimit + 5; i++)
            await _room.SayAsync(2, ":probe");

        Pending.Should().HaveCount(_room.FloodLimit);
    }
}
