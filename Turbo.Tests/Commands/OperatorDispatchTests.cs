using FluentAssertions;
using Turbo.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>
/// What the room does with a line that names an operator command: the cheap checks it can make
/// from its own copy, then out of its turn to the runner, which does the rest.
/// </summary>
public class OperatorDispatchTests
{
    private readonly CommandRoomFixture _room = new();
    private readonly IOperatorCommandRunner _runner;

    public OperatorDispatchTests()
    {
        _runner = _room.Harness.Fakes.Create<IOperatorCommandRunner>();
        RoomHarness.SetField(_room.Harness.Room, "_operatorCommandRunner", _runner);
        _room.Commands.Register([new OperatorProbeCommand()]);
    }

    private IEnumerable<FakeCall> Runs => _room.Harness.Fakes.Log.Of("RunAsync");

    [Fact]
    public async Task AnAllowedLine_LeavesTheTurn_ForTheRunner_WhichIsToldWhoAndWhereAndWhoElseWasThere()
    {
        _room.AddPlayer(2, "command.opprobe");
        _room.AddPlayer(3);
        _room.AddPlayer(4);

        var handled = await _room.SayAsync(2, ":opprobe alice 7d");

        handled.Should().BeTrue();
        var run = Runs.Should().ContainSingle().Subject;
        ((CommandDescriptor)run.Args[0]!).Name.Should().Be("opprobe");
        var executor = (PlayerOperatorExecutor)run.Args[1]!;
        executor.PlayerId!.Value.Value.Should().Be(2);
        executor.Name.Should().Be("player2");
        executor.RoomId.Should().Be(_room.Harness.Room.RoomId);
        executor.RoomPlayerIds.Select(x => x.Value).Should().BeEquivalentTo([2, 3, 4]);
        run.Args[2].Should().Be(" alice 7d");
        // The room has checked the node against its own copy, so the runner need not ask again.
        run.Args[3].Should().Be(false);
        _room.SaidToRoom().Should().BeEmpty();
    }

    [Fact]
    public async Task APlayerWithoutTheNode_IsRefusedByTheRoom_AndTheRunnerIsNeverCalled()
    {
        _room.AddPlayer(2);

        await _room.SayAsync(2, ":opprobe alice");

        Runs.Should().BeEmpty();
        _room.WhispersTo(2).Should().Equal("You can't use that command.");
        _room.SaidToRoom().Should().BeEmpty();
    }

    [Fact]
    public async Task ALineOverTheFloodLimit_IsDropped_WithoutReachingTheRunner()
    {
        _room.AddPlayer(2, "command.opprobe");

        for (var i = 0; i <= _room.FloodLimit; i++)
            await _room.SayAsync(2, ":opprobe alice");

        Runs.Count().Should().BeLessThanOrEqualTo(_room.FloodLimit);
        _room.WhispersTo(2).Should().Contain("You're sending commands too fast.");
    }

    [Fact]
    public async Task TheRoomDoesNotWriteTheUseDown_BecauseTheRunnerDoes_AfterItRan()
    {
        _room.AddPlayer(2, "command.opprobe", PermissionNodes.Command.LOG);

        await _room.SayAsync(2, ":opprobe alice");

        (
            (System.Collections.ICollection)
                RoomHarness.GetMember(_room.Harness.State, "PendingCommandLogs")!
        )
            .Count.Should()
            .Be(0);
    }
}
