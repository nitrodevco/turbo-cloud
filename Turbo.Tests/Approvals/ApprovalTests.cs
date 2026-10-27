using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Approvals;
using Turbo.Contracts.Hotel;
using Turbo.Plugins.Approvals;
using Turbo.Primitives.Events;
using Turbo.Testing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Approvals;

/// <summary>
/// Human approval (<c>docs/approvals.md</c>): a request waits, saved, until a staff member holding
/// its node approves or denies it or it expires; nobody decides their own; an approved one is
/// carried out once however many press approve, survives a restart, and a handler that fails
/// leaves the request failed with the reason, not an exception.
/// </summary>
public sealed class ApprovalTests : IDisposable
{
    private const string NODE = "plugin.shop.refund";

    private readonly SqliteDb _db = new();
    private readonly FakeHotel _hotel = new();
    private readonly ManualTimeProvider _clock = new();
    private readonly Events _events = new();
    private readonly RefundHandler _refunds = new();
    private readonly CapturingLogger<HotelApprovals> _log = new();
    private readonly FakePlayer _asker;
    private readonly FakePlayer _approver;

    public ApprovalTests()
    {
        _asker = _hotel.Players.Add("Asker");
        _approver = _hotel.Players.Add("Approver");
        _asker.Permissions.Add(NODE);
        _approver.Permissions.Add(NODE);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task An_approved_request_is_carried_out_once_with_its_arguments_and_the_approvers_note()
    {
        var approvals = Approvals();
        var asked = await approvals.RequestAsync(Refund(5000), Ct);

        asked.State.Should().Be(ApprovalState.Pending);
        _refunds.Runs.Should().BeEmpty();

        var decided = await approvals.ApproveAsync(asked.Id, _approver.Id, "ok, checked", Ct);

        decided.Status.Should().Be(ApprovalDecisionStatus.Executed);
        decided.Message.Should().Be("Approved and done: Refunded 5000.");
        _refunds.Runs.Should().Equal(new RefundArgs("Alice", 5000));

        var saved = (await approvals.GetAsync(asked.Id, Ct))!;

        (saved.State, saved.DecidedBy, saved.Note, saved.Result)
            .Should()
            .Be((ApprovalState.Executed, _approver.Id, "ok, checked", "Refunded 5000."));
        _events
            .Published.Select(x => x.GetType().Name)
            .Should()
            .Equal(
                nameof(ApprovalRequestedEvent),
                nameof(ApprovalDecidedEvent),
                nameof(ApprovalExecutedEvent)
            );
    }

    [Fact]
    public async Task A_second_approve_or_deny_of_a_decided_request_runs_nothing()
    {
        var approvals = Approvals();
        var asked = await approvals.RequestAsync(Refund(10), Ct);
        var other = _hotel.Players.Add("Other");

        other.Permissions.Add(NODE);

        (await approvals.ApproveAsync(asked.Id, _approver.Id, null, Ct))
            .Status.Should()
            .Be(ApprovalDecisionStatus.Executed);
        var again = await approvals.ApproveAsync(asked.Id, other.Id, null, Ct);
        var deny = await approvals.DenyAsync(asked.Id, other.Id, null, Ct);

        (again.Status, again.Message)
            .Should()
            .Be((ApprovalDecisionStatus.NotPending, "Someone approved it already."));
        deny.Status.Should().Be(ApprovalDecisionStatus.NotPending);
        _refunds.Runs.Should().HaveCount(1);
        _events.Of<ApprovalDecidedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Two_approvals_at_once_carry_it_out_once()
    {
        var approvals = Approvals();
        var asked = await approvals.RequestAsync(Refund(10), Ct);
        var other = _hotel.Players.Add("Other");

        other.Permissions.Add(NODE);
        // The second approver presses while the first one's handler is still running.
        _refunds.During = () => approvals.ApproveAsync(asked.Id, other.Id, null, Ct);

        var first = await approvals.ApproveAsync(asked.Id, _approver.Id, null, Ct);

        first.Status.Should().Be(ApprovalDecisionStatus.Executed);
        _refunds.DuringResult!.Status.Should().Be(ApprovalDecisionStatus.NotPending);
        _refunds.Runs.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_denied_request_never_runs_and_keeps_who_denied_it_and_why()
    {
        var approvals = Approvals();
        var asked = await approvals.RequestAsync(Refund(10), Ct);

        var denied = await approvals.DenyAsync(asked.Id, _approver.Id, "not a real loss", Ct);

        denied.Status.Should().Be(ApprovalDecisionStatus.Denied);
        (denied.Approval!.State, denied.Approval.DecidedBy, denied.Approval.Note)
            .Should()
            .Be((ApprovalState.Denied, _approver.Id, "not a real loss"));
        (await approvals.ApproveAsync(asked.Id, _approver.Id, null, Ct))
            .Message.Should()
            .Be("Someone denied it already.");
        _refunds.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task Nobody_decides_a_request_they_asked_for_or_that_was_asked_for_them()
    {
        var approvals = Approvals();
        var asked = await approvals.RequestAsync(Refund(10), Ct);

        var own = await approvals.ApproveAsync(asked.Id, _asker.Id, null, Ct);
        var ownDeny = await approvals.DenyAsync(asked.Id, _asker.Id, null, Ct);

        own.Status.Should().Be(ApprovalDecisionStatus.OwnRequest);
        ownDeny.Status.Should().Be(ApprovalDecisionStatus.OwnRequest);
        (await approvals.GetAsync(asked.Id, Ct))!.State.Should().Be(ApprovalState.Pending);
        _refunds.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task Deciding_needs_the_node_the_request_names()
    {
        var approvals = Approvals();
        var asked = await approvals.RequestAsync(Refund(10), Ct);
        var helper = _hotel.Players.Add("Helper");

        helper.Permissions.Add(
            Turbo.Primitives.Players.Permissions.PermissionNodes.Approvals.DECIDE
        );

        var refused = await approvals.ApproveAsync(asked.Id, helper.Id, null, Ct);

        (refused.Status, refused.Message)
            .Should()
            .Be(
                (
                    ApprovalDecisionStatus.NotPermitted,
                    $"Deciding this needs {NODE}, which you don't hold."
                )
            );
        _refunds.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task A_request_nobody_decides_in_time_expires_once_and_can_no_longer_be_approved()
    {
        var approvals = Approvals();
        var asked = await approvals.RequestAsync(
            Refund(10) with
            {
                ExpiresIn = TimeSpan.FromHours(1),
            },
            Ct
        );

        _clock.Advance(TimeSpan.FromMinutes(59));
        (await approvals.ExpireDueAsync(Ct)).Should().Be(0);
        _clock.Advance(TimeSpan.FromMinutes(1));

        (await approvals.ExpireDueAsync(Ct)).Should().Be(1);
        (await approvals.ExpireDueAsync(Ct)).Should().Be(0);

        var late = await approvals.ApproveAsync(asked.Id, _approver.Id, null, Ct);

        (late.Status, late.Message)
            .Should()
            .Be((ApprovalDecisionStatus.NotPending, "It expired before anyone decided."));
        _events
            .Of<ApprovalDecidedEvent>()
            .Single()
            .Approval.State.Should()
            .Be(ApprovalState.Expired);
        (await approvals.CountPendingAsync(Ct)).Should().Be(0);
        _refunds.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task A_request_survives_a_restart_and_is_carried_out_by_the_new_servers_handler()
    {
        var asked = await Approvals().RequestAsync(Refund(42), Ct);
        var restarted = Approvals(new RefundHandler());

        var read = (await restarted.GetAsync(asked.Id, Ct))!;

        (read.State, read.Summary, read.Requester, read.Node, read.ExpiresAtUtc)
            .Should()
            .Be(
                (
                    ApprovalState.Pending,
                    "Refund Alice 42 credits",
                    asked.Requester,
                    NODE,
                    asked.ExpiresAtUtc
                )
            );
        read.ArgumentsAs<RefundArgs>().Should().Be(new RefundArgs("Alice", 42));

        var decided = await restarted.ApproveAsync(asked.Id, _approver.Id, null, Ct);

        decided.Status.Should().Be(ApprovalDecisionStatus.Executed);
        _refunds.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task With_nothing_to_carry_its_kind_out_an_approval_is_refused_and_it_stays_pending()
    {
        var asked = await Approvals().RequestAsync(Refund(10), Ct);
        var withoutHandler = Approvals(handler: null);

        var refused = await withoutHandler.ApproveAsync(asked.Id, _approver.Id, null, Ct);

        refused.Status.Should().Be(ApprovalDecisionStatus.NoHandler);
        (await withoutHandler.GetAsync(asked.Id, Ct))!.State.Should().Be(ApprovalState.Pending);
    }

    [Fact]
    public async Task A_handler_that_throws_leaves_the_request_failed_with_a_reason_and_the_error_logged()
    {
        var approvals = Approvals();
        var asked = await approvals.RequestAsync(Refund(10), Ct);

        _refunds.Throw = true;

        var decided = await approvals.ApproveAsync(asked.Id, _approver.Id, null, Ct);

        decided.Status.Should().Be(ApprovalDecisionStatus.Failed);
        decided.Approval!.State.Should().Be(ApprovalState.Failed);
        decided
            .Approval.Result.Should()
            .Be("Carrying out shop.refund failed on the server; its error is in the server's log.");
        _events.Of<ApprovalExecutedEvent>().Single().Succeeded.Should().BeFalse();
        _log.AtLeast(LogLevel.Error)
            .Single()
            .Exception!.Message.Should()
            .Be("payment service down");
    }

    private HotelApprovals Approvals() => Approvals(_refunds);

    private HotelApprovals Approvals(IApprovalHandler? handler)
    {
        var host = new ServiceCollection()
            .AddSingleton<IHotel>(_hotel)
            .AddSingleton<IEventPublisher>(_events)
            .BuildServiceProvider();

        return new HotelApprovals(
            _db,
            new ApprovalHandlers(handler is null ? [] : [handler]),
            host,
            _clock,
            _log
        );
    }

    private ApprovalRequest Refund(int credits) =>
        new()
        {
            Kind = "shop.refund",
            Summary = $"Refund Alice {credits} credits",
            Arguments = new RefundArgs("Alice", credits),
            Requester = ApprovalRequester.Staff(_asker.Id),
            Node = NODE,
        };

    public sealed record RefundArgs(string Player, int Credits);

    private sealed class RefundHandler : ApprovalHandler<RefundArgs>
    {
        public ConcurrentQueue<RefundArgs> Runs { get; } = new();

        public bool Throw { get; set; }

        public Func<Task<ApprovalDecision>>? During { get; set; }

        public ApprovalDecision? DuringResult { get; private set; }

        public override string Kind => "shop.refund";

        public override async Task<ApprovalResult> ExecuteAsync(
            RefundArgs arguments,
            Approval approval,
            CancellationToken ct
        )
        {
            if (Throw)
                throw new InvalidOperationException("payment service down");

            if (During is { } during)
            {
                During = null;
                DuringResult = await during();
            }

            Runs.Enqueue(arguments);

            return ApprovalResult.Done($"Refunded {arguments.Credits}.");
        }
    }

    private sealed class Events : IEventPublisher
    {
        public ConcurrentQueue<IEvent> Published { get; } = new();

        public IEnumerable<T> Of<T>() => Published.OfType<T>();

        public Task PublishAsync(IEvent env, CancellationToken ct = default)
        {
            Published.Enqueue(env);

            return Task.CompletedTask;
        }

        public void PublishCommitted(DomainEvent evt) => Published.Enqueue(evt);
    }
}
