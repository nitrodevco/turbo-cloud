using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Commands.Events;
using Turbo.Primitives.Players.Permissions;
using Xunit;

namespace Turbo.Tests.Commands;

public sealed record OperatorProbeArguments(PlayerTarget Who, CommandDuration? Duration = null);

/// <summary>Resolves one player and says who it was and for how long.</summary>
[Command("opprobe", Description = "Probe")]
[RequiresPermission("command.opprobe")]
public sealed class OperatorProbeCommand : IOperatorCommand<OperatorProbeArguments>
{
    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string> { ["done"] = "Probed %0% for %1%." };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        OperatorProbeArguments arguments,
        CancellationToken ct
    )
    {
        var selection = await ctx.ResolveAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        return CommandResult.Done(
            "done",
            selection.Players[0].Name,
            arguments.Duration?.ToString() ?? "no time"
        );
    }
}

public sealed record OperatorMassArguments(
    [Selectors(OperatorMassCommand.MASS_NODE)] PlayerTarget Who
);

/// <summary>Aims at a group of players when the executor may.</summary>
[Command("opmass")]
[RequiresPermission("command.opmass")]
public sealed class OperatorMassCommand : IOperatorCommand<OperatorMassArguments>
{
    public const string MASS_NODE = "command.opmass.mass";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string> { ["done"] = "Reached %0%." };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        OperatorMassArguments arguments,
        CancellationToken ct
    )
    {
        var selection = await ctx.SelectAsync(arguments.Who, ct);

        return selection.Failure
            ?? CommandResult.Done(
                "done",
                string.Join(",", selection.Players.Select(x => x.Name).Order())
            );
    }
}

[Command("opboom")]
[RequiresPermission("command.opboom")]
public sealed class OperatorThrowingCommand : IOperatorCommand<NoArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) => throw new InvalidOperationException("boom");
}

public class OperatorCommandRunnerTests
{
    private readonly OperatorFixture _hotel = new();

    public OperatorCommandRunnerTests()
    {
        _hotel
            .WithPlayer(10, "Alice")
            .WithPlayer(11, "Bob")
            .WithPlayer(12, "Carol", online: false)
            .WithPlayer(2, "staff");
        _hotel.Commands.Register([
            new OperatorProbeCommand(),
            new OperatorMassCommand(),
            new OperatorThrowingCommand(),
        ]);
    }

    private static FakeExecutor Staff(params string[] nodes) =>
        new(2, "staff", roomId: 7, roomPlayers: [2, 10, 11], nodes);

    [Fact]
    public async Task ARunnableCommand_AnswersWithItsOwnText_NamingThePlayerAsTheHotelSpellsIt()
    {
        var staff = Staff("command.opprobe");

        var outcome = await _hotel.RunAsync("opprobe", staff, " alice 7d");

        outcome.Should().Be(CommandOutcome.Completed);
        staff.Replies.Should().Equal("Probed Alice for 7 d.");
    }

    [Fact]
    public async Task AHotelText_WinsOverTheCommandsDefault()
    {
        _hotel.HotelTexts["command.opprobe.done"] = "Looked at %0%.";
        var staff = Staff("command.opprobe");

        await _hotel.RunAsync("opprobe", staff, "bob");

        staff.Replies.Should().Equal("Looked at Bob.");
    }

    [Fact]
    public async Task ACommandReturningFailure_IsFailedInItsOutcome_EventAndLog()
    {
        var staff = Staff("command.opprobe", PermissionNodes.Command.LOG);
        _hotel.Events.Record<CommandExecutedEvent>();

        var outcome = await _hotel.RunAsync("opprobe", staff, "missing");

        outcome.Should().Be(CommandOutcome.Failed);
        _hotel
            .Events.Of<CommandExecutedEvent>()
            .Single()
            .Outcome.Should()
            .Be(CommandOutcome.Failed);
        await using var ctx = _hotel.Db.CreateDbContext();
        ctx.CommandLogs.Should().ContainSingle().Which.Outcome.Should().Be("failed");
    }

    [Fact]
    public async Task APlayerWithoutTheNode_IsRefused_AndTheCommandDoesNotRun()
    {
        var staff = Staff();

        var outcome = await _hotel.RunAsync("opprobe", staff, "alice");

        outcome.Should().Be(CommandOutcome.Refused);
        staff.Replies.Should().Equal("You can't use that command.");
    }

    [Fact]
    public async Task ARoomDispatch_RechecksCurrentAuthorityEvenIfItPreviouslyCheckedTheNode()
    {
        var staff = Staff();

        var outcome = await _hotel.RunAsync("opprobe", staff, "alice", checkNode: false);

        outcome.Should().Be(CommandOutcome.Refused);
    }

    [Fact]
    public async Task ABadDuration_AnswersWhatWasWrong_WithTheGeneratedUsage()
    {
        var staff = Staff("command.opprobe");

        var outcome = await _hotel.RunAsync("opprobe", staff, "alice 7");

        outcome.Should().Be(CommandOutcome.BindFailed);
        staff.Replies.Should().Equal("7 is not a valid duration. Usage: :opprobe <who> [duration]");
    }

    [Fact]
    public async Task NoArguments_AnswersWithUsage()
    {
        var staff = Staff("command.opprobe");

        await _hotel.RunAsync("opprobe", staff, "");

        staff.Replies.Should().Equal("Usage: :opprobe <who> [duration]");
    }

    [Fact]
    public async Task APlayerNobodyKnows_IsNamedInTheReply()
    {
        var staff = Staff("command.opprobe");

        await _hotel.RunAsync("opprobe", staff, "mallory");

        staff.Replies.Should().Equal("There is no player called mallory.");
    }

    [Fact]
    public async Task AnOfflinePlayer_CanStillBeNamed()
    {
        var staff = Staff("command.opprobe");

        await _hotel.RunAsync("opprobe", staff, "carol");

        staff.Replies.Should().Equal("Probed Carol for no time.");
    }

    [Fact]
    public async Task ASelector_IsNotAPlayer_ForACommandThatNamesOne()
    {
        var staff = Staff("command.opprobe");

        await _hotel.RunAsync("opprobe", staff, "@online");

        staff.Replies.Should().Equal("You can't use @online with that command.");
    }

    [Fact]
    public async Task ACommandThatThrows_IsContained_LoggedAndAnsweredGenerically()
    {
        var staff = Staff("command.opboom");

        var outcome = await _hotel.RunAsync("opboom", staff, "");

        outcome.Should().Be(CommandOutcome.Error);
        staff.Replies.Should().Equal("That command failed.");
        _hotel
            .Log.AtLeast(LogLevel.Error)
            .Should()
            .ContainSingle()
            .Which.Exception.Should()
            .BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task APluginMayVeto_ARoomPlayersCommand_ButIsNotAskedAboutTheConsole()
    {
        _hotel.Events.On<CommandExecutingEvent>(e => e.Cancel());
        var staff = Staff("command.opprobe");

        var vetoed = await _hotel.RunAsync("opprobe", staff, "alice");
        var console = new FakeExecutor(null, "console");
        var ran = await _hotel.RunAsync("opprobe", console, "alice");

        vetoed.Should().Be(CommandOutcome.Vetoed);
        ran.Should().Be(CommandOutcome.Completed);
        console.Replies.Should().Equal("Probed Alice for no time.");
    }

    [Fact]
    public async Task APlayersUse_IsPublished_AsExecuted_WithTheOutcome()
    {
        _hotel.Events.Record<CommandExecutedEvent>();
        var staff = Staff("command.opprobe");

        await _hotel.RunAsync("opprobe", staff, "alice");

        var executed = _hotel.Events.Of<CommandExecutedEvent>().Should().ContainSingle().Subject;
        executed.Outcome.Should().Be(CommandOutcome.Completed);
        executed.PlayerId.Value.Should().Be(2);
        executed.RoomId.Value.Should().Be(7);
    }

    [Fact]
    public async Task AUse_IsLogged_OnlyWhenTheExecutorHoldsCommandLog()
    {
        await _hotel.RunAsync("opprobe", Staff("command.opprobe"), "alice");
        await _hotel.RunAsync(
            "opprobe",
            Staff("command.opprobe", PermissionNodes.Command.LOG),
            "bob 2h"
        );

        await using var ctx = _hotel.Db.CreateDbContext();
        var rows = await ctx.CommandLogs.ToListAsync();

        rows.Should()
            .ContainSingle()
            .Which.Should()
            .Match<Turbo.Database.Entities.Room.CommandLogEntity>(x =>
                x.Command == "opprobe"
                && x.Arguments == "bob 2h"
                && x.Outcome == "completed"
                && x.PlayerEntityId == 2
                && x.RoomEntityId == 7
            );
    }

    [Fact]
    public async Task AGroupSelector_IsAlwaysLogged_EvenForSomeoneWhoDoesNotHoldCommandLog()
    {
        var staff = Staff("command.opmass", OperatorMassCommand.MASS_NODE);

        await _hotel.RunAsync("opmass", staff, "@online");

        await using var ctx = _hotel.Db.CreateDbContext();
        (await ctx.CommandLogs.ToListAsync())
            .Should()
            .ContainSingle()
            .Which.Arguments.Should()
            .Be("@online");
    }

    [Fact]
    public async Task ASelector_NeedsItsOwnNode()
    {
        var staff = Staff("command.opmass");

        await _hotel.RunAsync("opmass", staff, "@room");

        staff.Replies.Should().Equal("You can't use @room with that command.");
        await using var ctx = _hotel.Db.CreateDbContext();
        (await ctx.CommandLogs.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AtRoom_IsTheRoomsPlayers_AsTheyWereWhenTheLineWasTyped_AndOnlineIsEveryone()
    {
        var staff = Staff("command.opmass", OperatorMassCommand.MASS_NODE);

        await _hotel.RunAsync("opmass", staff, "@room");
        await _hotel.RunAsync("opmass", staff, "@online");

        // The room held the executor and two players; the whole hotel is those three, Carol being
        // offline.
        staff.Replies.Should().Equal("Reached Alice,Bob,staff.", "Reached Alice,Bob,staff.");
    }

    [Fact]
    public async Task AtRoom_FromTheConsole_NeedsARoom()
    {
        var console = new FakeExecutor(null, "console");

        await _hotel.RunAsync("opmass", console, "@room");

        console.Replies.Should().Equal("That command only works in a room.");
    }

    [Fact]
    public async Task ASelectorThatIsNoGroup_SaysSo()
    {
        var staff = Staff("command.opmass", OperatorMassCommand.MASS_NODE);

        await _hotel.RunAsync("opmass", staff, "@everyone");

        staff.Replies.Should().Equal("@everyone is not a group of players. Use @room or @online.");
    }

    [Theory]
    [InlineData("opprobe alice")]
    [InlineData(":opprobe alice")]
    [InlineData("  :OPPROBE   alice ")]
    public async Task TheConsole_RunsAWholeLine_WithOrWithoutTheColon(string line)
    {
        var console = new FakeExecutor(null, "console");

        var ran = await _hotel.Runner.TryRunLineAsync(line, console, CancellationToken.None);

        ran.Should().BeTrue();
        console.Replies.Should().Equal("Probed Alice for no time.");
    }

    [Fact]
    public async Task TheConsole_IsNotToldAboutALineThatIsNoCommand_SoItCanSayUnknown()
    {
        var console = new FakeExecutor(null, "console");

        (await _hotel.Runner.TryRunLineAsync("nothing here", console, CancellationToken.None))
            .Should()
            .BeFalse();
        console.Replies.Should().BeEmpty();
    }

    [Fact]
    public async Task TheConsole_IsToldARoomCommandNeedsARoom()
    {
        _hotel.Commands.Register([new HelloCommand()]);
        var console = new FakeExecutor(null, "console");

        var ran = await _hotel.Runner.TryRunLineAsync("hello", console, CancellationToken.None);

        ran.Should().BeTrue();
        console.Replies.Should().Equal("That command only works in a room.");
    }

    [Fact]
    public void AnOperatorCommand_CannotTakeARoomPlayer_NorARoomCommandAPlayerTarget()
    {
        var provider = new CommandRegistryProvider(
            new Turbo.Tests.Support.CapturingLogger<ICommandRegistryProvider>()
        );

        FluentActions
            .Invoking(() => provider.Register([new OperatorWithRoomPlayerCommand()]))
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*operator commands take a PlayerTarget*");
        FluentActions
            .Invoking(() => provider.Register([new RoomWithTargetCommand()]))
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*only operator commands take a PlayerTarget*");
    }
}

public sealed record WithRoomPlayerArguments(Turbo.Primitives.Rooms.Object.Avatars.IRoomPlayer Who);

[Command("oproomplayer")]
[RequiresPermission("command.oproomplayer")]
public sealed class OperatorWithRoomPlayerCommand : IOperatorCommand<WithRoomPlayerArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        WithRoomPlayerArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}

[Command("roomtarget")]
[RequiresPermission("command.roomtarget")]
public sealed class RoomWithTargetCommand : ICommand<OperatorMassArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        OperatorMassArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}
