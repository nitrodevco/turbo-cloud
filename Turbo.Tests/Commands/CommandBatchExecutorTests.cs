using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Commands;
using Turbo.Primitives.Commands;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

public class CommandBatchExecutorTests
{
    private readonly CapturingLogger<ICommandBatchExecutor> _log = new();

    private CommandBatchExecutor Executor(int concurrency = 2) =>
        new(Options.Create(new CommandConfig { MaxBatchConcurrency = concurrency }), _log);

    private static ResolvedPlayer Player(int id) => new(id, $"p{id}");

    [Fact]
    public async Task AThrownCall_KeepsPartialQuantity_ContinuesOtherPlayers_AndIsNotRetried()
    {
        var calls = new ConcurrentDictionary<int, int>();
        var result = await Executor()
            .ExecuteAsync(
                "batchprobe",
                [Player(1), Player(2)],
                3,
                (player, _) =>
                {
                    var call = calls.AddOrUpdate(player.Id.Value, 1, (_, old) => old + 1);
                    if (player.Id.Value == 1 && call == 2)
                        throw new InvalidOperationException("May have committed");
                    return ValueTask.FromResult(true);
                },
                TestContext.Current.CancellationToken
            );

        result.Outcome.Should().Be(CommandOutcome.Partial);
        result.Targets[0].Should().Be(new CommandBatchTargetResult(1, 3, 1, 0, 1));
        result.Targets[0].Unattempted.Should().Be(1);
        result.Targets[1].Completed.Should().BeTrue();
        calls[1].Should().Be(2);
        calls[2].Should().Be(3);
        _log.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task Concurrency_IsBounded_AndResultsKeepTargetOrder()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var entered = 0;
        var maximum = 0;
        var run = Executor()
            .ExecuteAsync(
                "batchprobe",
                [Player(3), Player(1), Player(2)],
                1,
                async (_, _) =>
                {
                    var count = Interlocked.Increment(ref active);
                    maximum = Math.Max(maximum, count);
                    if (Interlocked.Increment(ref entered) == 2)
                        started.TrySetResult();
                    await release.Task.WaitAsync(TestContext.Current.CancellationToken);
                    Interlocked.Decrement(ref active);
                    return true;
                },
                TestContext.Current.CancellationToken
            );
        try
        {
            await started.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            entered.Should().Be(2);
        }
        finally
        {
            release.TrySetResult();
        }

        var result = await run;
        maximum.Should().Be(2);
        result.Targets.Select(x => x.PlayerId.Value).Should().Equal(3, 1, 2);
        result.Outcome.Should().Be(CommandOutcome.Completed);
    }

    [Fact]
    public async Task Cancellation_AfterSuccess_ReportsPartialAndUnattemptedWork()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        var result = await Executor(1)
            .ExecuteAsync(
                "batchprobe",
                [Player(1), Player(2)],
                2,
                async (_, _) =>
                {
                    await cancellation.CancelAsync();
                    return true;
                },
                cancellation.Token
            );

        result.Outcome.Should().Be(CommandOutcome.Partial);
        result.Succeeded.Should().Be(1);
        result.Unattempted.Should().Be(3);
        result.UnattemptedTargets.Should().Be(1);
        result.WasCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task Cancellation_DuringACall_MarksItIndeterminate_AndDoesNotRetry()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        var calls = 0;
        var result = await Executor(1)
            .ExecuteAsync(
                "batchprobe",
                [Player(1), Player(2)],
                3,
                async (_, token) =>
                {
                    calls++;
                    await cancellation.CancelAsync();
                    throw new OperationCanceledException(token);
                },
                cancellation.Token
            );

        result.Outcome.Should().Be(CommandOutcome.Canceled);
        result.Indeterminate.Should().Be(1);
        result.Unattempted.Should().Be(5);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Cancellation_BeforeStarting_LeavesEveryTargetUnattempted()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var result = await Executor()
            .ExecuteAsync(
                "batchprobe",
                [Player(1)],
                2,
                (_, _) => throw new InvalidOperationException("Must not run"),
                cancellation.Token
            );

        result.Outcome.Should().Be(CommandOutcome.Canceled);
        result.Unattempted.Should().Be(2);
        result.Indeterminate.Should().Be(0);
    }

    [Fact]
    public async Task DuplicatePlayers_AreExecutedOnce_AndRefusalsAreFailed()
    {
        var calls = 0;
        var result = await Executor()
            .ExecuteAsync(
                "batchprobe",
                [Player(1), Player(1)],
                1,
                (_, _) =>
                {
                    calls++;
                    return ValueTask.FromResult(false);
                },
                TestContext.Current.CancellationToken
            );

        result.Targets.Should().ContainSingle();
        calls.Should().Be(1);
        result.Outcome.Should().Be(CommandOutcome.Failed);
        result.Failed.Should().Be(1);
    }
}
