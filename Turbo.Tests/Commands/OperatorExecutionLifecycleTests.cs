// Test signals deliberately coordinate concurrently executing commands; they do not use the UI joinable-task context.
#pragma warning disable VSTHRD003
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

public class OperatorExecutionLifecycleTests
{
    [Command("executionprobe")]
    [RequiresPermission("command.executionprobe")]
    private sealed class ExecutionProbeCommand : IOperatorCommand<NoArguments>
    {
        public Func<
            IOperatorCommandContext,
            CancellationToken,
            ValueTask<CommandResult>
        > Run { get; init; } = (_, _) => ValueTask.FromResult(CommandResult.Ok);

        public ValueTask<CommandResult> ExecuteAsync(
            IOperatorCommandContext ctx,
            NoArguments arguments,
            CancellationToken ct
        ) => Run(ctx, ct);
    }

    private sealed class ThrowingArguments
    {
        public ThrowingArguments(string value) => throw new InvalidOperationException(value);
    }

    [Command("constructorprobe")]
    [RequiresPermission("command.constructorprobe")]
    private sealed class ConstructorProbeCommand : IOperatorCommand<ThrowingArguments>
    {
        public ValueTask<CommandResult> ExecuteAsync(
            IOperatorCommandContext ctx,
            ThrowingArguments arguments,
            CancellationToken ct
        ) => throw new InvalidOperationException("Execution must not be reached");
    }

    private static TaskCompletionSource Signal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task SameExecutor_PreservesAdmissionOrder_UntilCanceledMutationActuallySettles()
    {
        var hotel = new OperatorFixture();
        using var runner = hotel.Runner;
        var firstStarted = Signal();
        var release = Signal();
        var order = new List<int>();
        var calls = 0;
        hotel.Commands.Register([
            new ExecutionProbeCommand
            {
                Run = async (_, _) =>
                {
                    var invocation = ++calls;
                    order.Add(invocation);
                    if (invocation == 1)
                    {
                        firstStarted.SetResult();
                        await release.Task;
                    }
                    order.Add(-invocation);
                    return CommandResult.Ok;
                },
            },
        ]);
        var console = new FakeExecutor(null, "console");
        using var cancellation = new CancellationTokenSource();
        var first = runner.RunAsync(
            hotel.Find("executionprobe"),
            console,
            "",
            true,
            cancellation.Token
        );
        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        var second = hotel.RunAsync("executionprobe", console, "");
        var third = hotel.RunAsync("executionprobe", console, "");
        await cancellation.CancelAsync();
        calls.Should().Be(1);
        first.IsCompleted.Should().BeFalse();
        second.IsCompleted.Should().BeFalse();
        third.IsCompleted.Should().BeFalse();
        release.SetResult();
        await Task.WhenAll(first, second, third);
        order.Should().Equal(1, -1, 2, -2, 3, -3);
    }

    [Theory]
    [InlineData(1, 8, true)]
    [InlineData(8, 1, false)]
    public async Task OutstandingAdmission_IsBounded(
        int globalLimit,
        int executorLimit,
        bool differentExecutor
    )
    {
        var hotel = new OperatorFixture(
            new CommandConfig
            {
                MaxOutstandingExecutions = globalLimit,
                MaxOutstandingExecutionsPerExecutor = executorLimit,
            }
        );
        using var runner = hotel.Runner;
        var started = Signal();
        var release = Signal();
        hotel.Commands.Register([
            new ExecutionProbeCommand
            {
                Run = async (_, _) =>
                {
                    started.TrySetResult();
                    await release.Task;
                    return CommandResult.Ok;
                },
            },
        ]);
        var executor = new FakeExecutor(null, "console");
        var first = hotel.RunAsync("executionprobe", executor, "");
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            var rejected = differentExecutor
                ? new FakeExecutor(2, "staff", nodes: "command.executionprobe")
                : executor;
            (await hotel.RunAsync("executionprobe", rejected, ""))
                .Should()
                .Be(CommandOutcome.Refused);
            rejected.Replies.Should().Contain(CommandReplyKeys.Defaults[CommandReplyKeys.BUSY]);
        }
        finally
        {
            release.TrySetResult();
        }
        await first;
    }

    [Fact]
    public async Task QueuedExecution_RechecksAuthorityAndRegistration()
    {
        var hotel = new OperatorFixture();
        using var runner = hotel.Runner;
        var started = Signal();
        var release = Signal();
        var calls = 0;
        using var registration = hotel.Commands.Register([
            new ExecutionProbeCommand
            {
                Run = async (_, _) =>
                {
                    calls++;
                    started.TrySetResult();
                    await release.Task;
                    return CommandResult.Ok;
                },
            },
        ]);
        var staff = new FakeExecutor(2, "staff", nodes: "command.executionprobe");
        var first = hotel.RunAsync("executionprobe", staff, "");
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        var queued = hotel.RunAsync("executionprobe", staff, "");
        staff.Revoke("command.executionprobe");
        release.SetResult();
        await first;
        (await queued).Should().Be(CommandOutcome.Refused);
        calls.Should().Be(1);
        var old = hotel.Find("executionprobe");
        registration.Dispose();
        (
            await runner.RunAsync(
                old,
                new FakeExecutor(null, "console"),
                "",
                true,
                CancellationToken.None
            )
        )
            .Should()
            .Be(CommandOutcome.Refused);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task ThrowingArgumentConstructor_IsContainedAndAudited()
    {
        var hotel = new OperatorFixture();
        using var runner = hotel.Runner;
        hotel.Commands.Register([new ConstructorProbeCommand()]);
        var console = new FakeExecutor(null, "console");
        (await hotel.RunAsync("constructorprobe", console, "boom"))
            .Should()
            .Be(CommandOutcome.Error);
        await using var db = hotel.Db.CreateDbContext();
        var audit = await db.CommandLogs.SingleAsync(TestContext.Current.CancellationToken);
        audit.Outcome.Should().Be("error");
        audit.ExecutionId.Should().NotBeNull();
        audit.Source.Should().Be("console");
        console.Replies.Should().Equal(CommandReplyKeys.Defaults[CommandReplyKeys.FAILED]);
    }

    [Fact]
    public async Task ExpiredConfirmations_AreRemovedWithoutAnotherInvocation_AndCapacityIsBounded()
    {
        var hotel = new OperatorFixture(
            new CommandConfig
            {
                MaxPendingConfirmations = 1,
                ConfirmationSeconds = 1,
                ConfirmationCleanupSeconds = 1,
            }
        );
        using var runner = hotel.Runner;
        hotel.Commands.Register([
            new ExecutionProbeCommand
            {
                Run = (_, _) =>
                    ValueTask.FromResult(
                        CommandResult.Confirm(CommandReplyKeys.CONFIRM_SELECTOR, "1")
                    ),
            },
        ]);
        var console = new FakeExecutor(null, "console");
        (await hotel.RunAsync("executionprobe", console, ""))
            .Should()
            .Be(CommandOutcome.AwaitingConfirmation);
        var other = new FakeExecutor(2, "staff", nodes: "command.executionprobe");
        (await hotel.RunAsync("executionprobe", other, "")).Should().Be(CommandOutcome.Refused);
        hotel.Clock.Advance(TimeSpan.FromSeconds(2));
        await Task.Delay(TimeSpan.FromMilliseconds(1200), TestContext.Current.CancellationToken);
        var pending = typeof(OperatorCommandRunner)
            .GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(runner)!;
        ((int)pending.GetType().GetProperty("Count")!.GetValue(pending)!).Should().Be(0);
        (await hotel.RunAsync("executionprobe", other, ""))
            .Should()
            .Be(CommandOutcome.AwaitingConfirmation);
    }

    [Fact]
    public async Task Audit_PreservesApprovedAudience_TargetAccounting_AndConfirmationLinks()
    {
        var hotel = new OperatorFixture();
        using var runner = hotel.Runner;
        hotel.Commands.Register([
            new ConfirmCommand(),
            new ExecutionProbeCommand
            {
                Run = async (ctx, ct) =>
                {
                    var ids = ctx.SnapshotRecipients("approved", [10, 11]);
                    if (!ctx.IsConfirmed)
                        return CommandResult.Confirm(CommandReplyKeys.CONFIRM_SELECTOR, "2");
                    var batch = await ctx.ExecuteBatchAsync(
                        ids.Select(id => new ResolvedPlayer(id, id.ToString())).ToArray(),
                        2,
                        (player, _) => ValueTask.FromResult(player.Id.Value == 10),
                        ct
                    );
                    return CommandResult.Ok with { Batch = batch };
                },
            },
        ]);
        var console = new FakeExecutor(null, "console");
        await hotel.RunAsync("executionprobe", console, "");
        await hotel.RunAsync("confirm", console, "");
        await using var db = hotel.Db.CreateDbContext();
        var rows = await db.CommandLogs.ToListAsync(TestContext.Current.CancellationToken);
        var prompt = rows.Single(x => x.Outcome == "confirm");
        var execution = rows.Single(x => x.Command == "executionprobe" && x.Outcome == "partial");
        var confirmation = rows.Single(x => x.Command == "confirm");
        prompt.ConfirmationId.Should().Be(prompt.ExecutionId);
        execution.ConfirmationId.Should().Be(prompt.ExecutionId);
        confirmation.ConfirmationId.Should().Be(prompt.ExecutionId);
        execution.ParentExecutionId.Should().Be(confirmation.ExecutionId);
        execution.ResolvedAudienceJson.Should().Be(prompt.ResolvedAudienceJson);
        using var targets = JsonDocument.Parse(execution.BatchTargetResultsJson!);
        targets.RootElement[0].GetProperty("PlayerId").GetInt32().Should().Be(10);
        targets.RootElement[0].GetProperty("Succeeded").GetInt32().Should().Be(2);
        targets.RootElement[1].GetProperty("Failed").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task ConcurrentBatches_ShareTheOperationBound()
    {
        var executor = new CommandBatchExecutor(
            Options.Create(new CommandConfig { MaxBatchConcurrency = 1 }),
            new CapturingLogger<ICommandBatchExecutor>()
        );
        var started = Signal();
        var release = Signal();
        var calls = 0;
        async ValueTask<bool> Operation(ResolvedPlayer _, CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            return true;
        }
        var first = executor.ExecuteAsync(
            "probe",
            [new ResolvedPlayer(1, "one")],
            1,
            Operation,
            CancellationToken.None
        );
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        var second = executor.ExecuteAsync(
            "probe",
            [new ResolvedPlayer(2, "two")],
            1,
            Operation,
            CancellationToken.None
        );
        calls.Should().Be(1);
        second.IsCompleted.Should().BeFalse();
        release.SetResult();
        await Task.WhenAll(first, second);
        calls.Should().Be(2);
    }
}
