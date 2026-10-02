using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Commands;
using Turbo.Database.Context;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Commands.Events;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Texts;
using Xunit;

namespace Turbo.Tests.Commands;

public class CommandFinalizationTests
{
    private static FakeExecutor Staff() =>
        new(2, "staff", 7, [2], "command.finalizationprobe", "command.log");

    [Fact]
    public async Task CompletionHooks_SeeTheAuditAlreadyWritten()
    {
        var hotel = new OperatorFixture();
        hotel.Commands.Register([new FinalizationProbeCommand()]);
        var audited = false;
        hotel.Events.On<CommandExecutedEvent>(_ =>
        {
            using var db = hotel.Db.CreateDbContext();
            audited = db.CommandLogs.Any(x =>
                x.Command == "finalizationprobe" && x.Outcome == "completed"
            );
        });

        var outcome = await hotel.RunAsync("finalizationprobe", Staff(), "");

        outcome.Should().Be(CommandOutcome.Completed);
        audited.Should().BeTrue();
    }

    [Fact]
    public async Task Cancellation_AfterExecution_DoesNotChangeSuccessOrSkipAudit()
    {
        var hotel = new OperatorFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        hotel.Commands.Register([
            new FinalizationProbeCommand { BeforeReturn = cancellation.Cancel },
        ]);
        hotel.Events.Record<CommandExecutedEvent>();

        var outcome = await hotel.Runner.RunAsync(
            hotel.Find("finalizationprobe"),
            Staff(),
            "",
            true,
            cancellation.Token
        );

        outcome.Should().Be(CommandOutcome.Completed);
        await using var db = hotel.Db.CreateDbContext();
        db.CommandLogs.Should().ContainSingle().Which.Outcome.Should().Be("completed");
        hotel
            .Events.Of<CommandExecutedEvent>()
            .Should()
            .ContainSingle()
            .Which.Outcome.Should()
            .Be(CommandOutcome.Completed);
    }

    [Fact]
    public async Task Cancellation_DuringExecution_IsAuditedBeforeItPropagates()
    {
        var hotel = new OperatorFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        hotel.Commands.Register([
            new FinalizationProbeCommand
            {
                BeforeReturn = cancellation.Cancel,
                ThrowCancellation = true,
            },
        ]);
        hotel.Events.Record<CommandExecutedEvent>();

        var run = () =>
            hotel.Runner.RunAsync(
                hotel.Find("finalizationprobe"),
                Staff(),
                "",
                true,
                cancellation.Token
            );
        await run.Should().ThrowAsync<OperationCanceledException>();

        await using var db = hotel.Db.CreateDbContext();
        db.CommandLogs.Should().ContainSingle().Which.Outcome.Should().Be("canceled");
        hotel
            .Events.Of<CommandExecutedEvent>()
            .Should()
            .ContainSingle()
            .Which.Outcome.Should()
            .Be(CommandOutcome.Canceled);
    }

    [Fact]
    public async Task CompletionHook_IgnoringCancellation_IsBounded_AndCannotChangeTheOutcome()
    {
        var hotel = new OperatorFixture(new CommandConfig { FinalizationTimeoutSeconds = 1 });
        hotel.Commands.Register([new FinalizationProbeCommand()]);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hotel.Events.OnAsync<CommandExecutedEvent>(
            (_, _) =>
            {
                started.TrySetResult();
                return new ValueTask(release.Task);
            }
        );
        var run = hotel.RunAsync("finalizationprobe", Staff(), "");
        try
        {
            await started.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            var outcome = await run.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            outcome.Should().Be(CommandOutcome.Completed);
            await using var db = hotel.Db.CreateDbContext();
            db.CommandLogs.Should().ContainSingle().Which.Outcome.Should().Be("completed");
            hotel
                .Log.AtLeast(LogLevel.Error)
                .Should()
                .ContainSingle()
                .Which.Message.Should()
                .Contain("completion hooks");
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuditFailureOrTimeout_IsLogged_AndDoesNotSuppressCompletionHooks(bool timeOut)
    {
        var hotel = new OperatorFixture(new CommandConfig { FinalizationTimeoutSeconds = 1 });
        hotel.Commands.Register([new FinalizationProbeCommand()]);
        hotel.Events.Record<CommandExecutedEvent>();
        hotel.Fakes.Handlers["CreateDbContextAsync"] = call =>
            timeOut
                ? WaitForCancellationAsync((CancellationToken)call.Args[0]!)
                : throw new InvalidOperationException("Database unavailable");
        var runner = new OperatorCommandRunner(
            hotel.Commands,
            hotel.Fakes.Create<IHotelTextProvider>(),
            hotel.Events.System,
            hotel.Fakes.Create<Orleans.IGrainFactory>(),
            hotel.Fakes.Create<ISessionGateway>(),
            new CommandBatchExecutor(
                Options.Create(hotel.Config),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ICommandBatchExecutor>.Instance
            ),
            hotel.Fakes.Create<Turbo.Primitives.Players.Notifications.IPlayerNoticeService>(),
            hotel.Fakes.Create<IDbContextFactory<TurboDbContext>>(),
            Options.Create(hotel.Config),
            hotel.Clock,
            hotel.Log
        );

        var outcome = await runner.RunAsync(
            hotel.Find("finalizationprobe"),
            Staff(),
            "",
            true,
            TestContext.Current.CancellationToken
        );

        outcome.Should().Be(CommandOutcome.Completed);
        hotel.Events.Of<CommandExecutedEvent>().Should().ContainSingle();
        hotel
            .Log.AtLeast(LogLevel.Error)
            .Should()
            .Contain(x => x.Message.Contains("command log") || x.Message.Contains("audit"));

        static async Task<TurboDbContext> WaitForCancellationAsync(CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("Must be canceled");
        }
    }
}
