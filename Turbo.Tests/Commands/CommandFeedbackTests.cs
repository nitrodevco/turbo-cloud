using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Commands;
using Turbo.Operations.Commands;
using Turbo.Players;
using Turbo.Players.Configuration;
using Turbo.Players.Notifications;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

public class CommandFeedbackTests
{
    [Fact]
    public async Task CurrencyRewardUsesCornerBubbleInsteadOfModeratorDialog()
    {
        var fakes = new Fakes();
        fakes.Handlers["TrySendComposerAsync"] = _ => Task.FromResult(true);
        var currency = new CurrencyTypeSnapshot
        {
            Id = 1,
            Name = "credits",
            CurrencyType = CurrencyType.Credits,
            Enabled = true,
        };

        (
            await Notices(fakes)
                .SendCurrencyRewardAsync(1, 50, currency, TestContext.Current.CancellationToken)
        )
            .Should()
            .Be(PlayerNoticeDelivery.Sent);

        var composer = fakes
            .Log.Of("TrySendComposerAsync")
            .Single()
            .Args[0]
            .Should()
            .BeOfType<NotificationDialogMessageComposer>()
            .Subject;
        composer.Parameters["display"].Should().Be("BUBBLE");
        composer.Parameters["message"].Should().Be("You received 50 credits.");
        composer.Parameters["image"].Should().Be("if_icon_temp_png");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplyFallsBackWhenExecutorHasLeftOrRoomLookupFails(bool throws)
    {
        var fakes = new Fakes();
        fakes.Handlers["WhisperToPlayerAsync"] = _ =>
            throws
                ? throw new InvalidOperationException("Room unavailable")
                : Task.FromResult(false);
        var executor = new PlayerOperatorExecutor(
            fakes.Create<IGrainFactory>(),
            1,
            "Alice",
            9,
            [],
            fakes.Create<IPlayerNoticeService>(),
            new CapturingLogger<PlayerOperatorExecutor>()
        );

        await executor.ReplyAsync("Saved.", TestContext.Current.CancellationToken);

        var notice = fakes.Log.On<IPlayerNoticeService>().Should().ContainSingle().Subject;
        ((PlayerId)notice.Args[0]!).Should().Be((PlayerId)1);
        ((IReadOnlyList<string>)notice.Args[3]!).Should().Equal("Saved.");
    }

    [Fact]
    public async Task TargetNoticeFailureDoesNotTurnAppliedRestrictionIntoFailure()
    {
        var hotel = new OperatorFixture().WithPlayer(2, "Alice");
        hotel.Commands.Register([
            new TradelockCommand(hotel.Fakes.Create<IGrainFactory>(), hotel.Clock),
        ]);
        hotel.Fakes.Handlers["SetNodeAsync"] = _ =>
            Task.FromResult(PermissionChangeResultType.Changed);
        hotel.Fakes.Handlers["TrySendComposerAsync"] = _ =>
            throw new InvalidOperationException("Notice unavailable");
        var executor = new FakeExecutor(null, "console");

        var outcome = await hotel.RunAsync("tradelock", executor, "Alice 1h");

        outcome.Should().Be(CommandOutcome.Completed);
        hotel.Fakes.Log.Of("SetNodeAsync").Should().ContainSingle();
        executor.Replies.Should().HaveCount(2);
        executor.Replies[0].Should().Contain("Alice can't trade");
        executor.Replies[1].Should().Contain("action result is unchanged");
    }

    [Fact]
    public async Task OfflineTargetStillChangesButExecutorIsToldNoticeWasSkipped()
    {
        var hotel = new OperatorFixture().WithPlayer(2, "Alice", online: false);
        hotel.Commands.Register([
            new TradelockCommand(hotel.Fakes.Create<IGrainFactory>(), hotel.Clock),
        ]);
        hotel.Fakes.Handlers["SetNodeAsync"] = _ =>
            Task.FromResult(PermissionChangeResultType.Changed);
        var executor = new FakeExecutor(null, "console");

        (await hotel.RunAsync("tradelock", executor, "Alice 1h"))
            .Should()
            .Be(CommandOutcome.Completed);

        executor.Replies.Should().Contain("No notice was sent to 1 offline player(s).");
        hotel.Fakes.Log.Of("SetNodeAsync").Should().ContainSingle();
    }

    [Fact]
    public async Task NoticeUsesHotelTextAndReturnsSubmissionOutcome()
    {
        var fakes = new Fakes();
        HotelTextFakes.Use(fakes, _ => "Changed: %0%.");
        fakes.Handlers["TrySendComposerAsync"] = _ => Task.FromResult(true);
        var service = Notices(fakes);

        (
            await service.SendAsync(
                1,
                "test.notice",
                "fallback",
                ["trading"],
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(PlayerNoticeDelivery.Sent);

        ((ModeratorMessageComposer)fakes.Log.Of("TrySendComposerAsync").Single().Args[0]!)
            .Message.Should()
            .Be("Changed: trading.");
    }

    [Fact]
    public async Task StalledNoticeIsBoundedAndDoesNotThrow()
    {
        var fakes = new Fakes();
        var pending = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
#pragma warning disable VSTHRD003 // Deliberately model a remote send that ignores cancellation; the service must bound its wait.
        fakes.Handlers["TrySendComposerAsync"] = _ => pending.Task;
#pragma warning restore VSTHRD003
        var service = Notices(fakes, timeoutMs: 10);
        try
        {
            (
                await service.SendAsync(
                    1,
                    "test.notice",
                    "Saved.",
                    [],
                    TestContext.Current.CancellationToken
                )
            )
                .Should()
                .Be(PlayerNoticeDelivery.Failed);
        }
        finally
        {
            pending.TrySetResult(false);
        }
    }

    [Fact]
    public async Task PresenceNeverQueuesNoticeForOfflineOrCanceledRequest()
    {
        var fakes = new Fakes();
        var presence = (IPlayerPresenceGrain)
            GrainHarness.Create(
                typeof(PlayerModule).Assembly,
                "Turbo.Players.Grains.PlayerPresenceGrain",
                fakes
            );
        var message = new ModeratorMessageComposer { Message = "Notice", Url = string.Empty };
        (await presence.TrySendComposerAsync(message, TestContext.Current.CancellationToken))
            .Should()
            .BeFalse();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> send = () => presence.TrySendComposerAsync(message, cancellation.Token);
        await send.Should().ThrowAsync<OperationCanceledException>();

        var state = presence
            .GetType()
            .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(presence)!;
        var queue =
            (Queue<IComposer>)state.GetType().GetProperty("OutgoingQueue")!.GetValue(state)!;
        queue.Should().BeEmpty();
    }

    private static PlayerNoticeService Notices(Fakes fakes, int timeoutMs = 5000) =>
        new(
            fakes.Create<IGrainFactory>(),
            fakes.Create<IHotelTextProvider>(),
            Options.Create(new PlayerConfig { NoticeTimeoutMs = timeoutMs }),
            new CapturingLogger<IPlayerNoticeService>()
        );
}
