using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Ai;
using Turbo.Ai.Approvals;
using Turbo.Ai.Configuration;
using Turbo.Ai.Drivers;
using Turbo.Ai.Storage;
using Turbo.Contracts.Ai;
using Turbo.Contracts.Approvals;
using Turbo.Contracts.Hotel;
using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Mcp;
using Turbo.Plugins.Approvals;
using Turbo.Primitives.Events;
using Turbo.Primitives.Players.Permissions;
using Turbo.Testing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Ai;

/// <summary>
/// An agent's tool call through the hotel's approvals (<c>docs/approvals.md</c>): the request is
/// the plugin's, asked by the agent for the staff member it acts for (who can't approve it
/// themself); another staff member's approval queues the run, which runs the tool and goes on in
/// the background, the agent told; a denial goes on without it.
/// </summary>
public sealed class AiApprovalsTests : IDisposable
{
    private static readonly PluginManifest DESK = new()
    {
        Name = "Desk",
        Key = "desk",
        Version = "1.0.0",
        Author = "test",
        Ai = new PluginAiUse { Purpose = "Kicks players when staff ask." },
    };

    private readonly SqliteDb _db = new();
    private readonly FakeHotel _hotel = new();
    private readonly Events _events = new();
    private readonly ConcurrentQueue<Func<Task<AiDriverAnswer>>> _model = new();
    private readonly List<AiResumed> _told = [];
    private readonly CapturingLogger<AiHost> _hostLog = new();
    private readonly ServiceProvider _services;

    public AiApprovalsTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IHotel>(_hotel)
            .AddSingleton<IEventPublisher>(_events)
            .AddSingleton(_told)
            .AddSingleton<IOptionsMonitor<AiConfig>>(
                new Monitor(
                    new AiConfig
                    {
                        Providers = new()
                        {
                            ["script"] = new()
                            {
                                Driver = "script",
                                Model = "m",
                                NoKey = true,
                            },
                        },
                        Plugins = ["desk"],
                        ResumeQueueCapacity = 1,
                    }
                )
            )
            .AddSingleton<IAiDriver>(new Script(_model))
            .AddSingleton<IAiStore>(new MemoryAiStore())
            .AddSingleton<IAiPermissions>(sp => new Permissions(_hotel))
            .AddSingleton<IAiApprovals>(sp => new HotelAiApprovals(sp))
            .AddSingleton<TimeProvider>(_hotel.Clock)
            .AddSingleton<IAiRateLimits>(sp => new AiRateLimits(_hotel.Clock))
            .AddSingleton(sp =>
                ActivatorUtilities.CreateInstance<AiProviders>(sp, NullLogger<AiProviders>.Instance)
            )
            .AddSingleton(sp =>
                ActivatorUtilities.CreateInstance<AiRuntime>(sp, NullLogger<AiRuntime>.Instance)
            )
            .AddSingleton(sp => new AiHost(sp.GetRequiredService<AiRuntime>(), _hostLog))
            .AddSingleton(sp => new ApprovalHandlers([
                new AiToolApprovalHandler(sp.GetRequiredService<AiHost>()),
            ]))
            .AddSingleton(sp => new HotelApprovals(
                _db,
                sp.GetRequiredService<ApprovalHandlers>(),
                sp,
                _hotel.Clock,
                NullLogger<HotelApprovals>.Instance
            ))
            .BuildServiceProvider();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _services.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Another_staff_members_approval_runs_the_tool_and_the_run_goes_on()
    {
        var (staff, approver, bob) = Players();
        var host = _services.GetRequiredService<AiHost>();
        using var live = host.Live(DESK, typeof(KickAgent).Assembly, _services);
        Reply(new AiToolCallPart("c1", "kick", Json($$"""{"player":{{bob.Id.Value}}}""")));
        Reply(new AiTextPart("Bob was kicked."));

        var paused = await host.ForPlugin(DESK)
            .PromptAsync(
                new KickAgent(_hotel, _told),
                "Kick Bob",
                new AiCall { As = AiActor.AsPlayer(staff.Id) },
                Ct
            );
        var approvals = _services.GetRequiredService<HotelApprovals>();
        var id = int.Parse(paused.Pending!.ApprovalId);
        var asked = (await approvals.GetAsync(id, Ct))!;

        (asked.Kind, asked.Plugin, asked.Requester.Kind, asked.Requester.OnBehalfOf, asked.Node)
            .Should()
            .Be(
                (
                    "ai.tool",
                    "desk",
                    ApprovalRequesterKind.Agent,
                    staff.Id,
                    PermissionNodes.Approvals.DECIDE
                )
            );
        (await approvals.ApproveAsync(id, staff.Id, null, Ct))
            .Status.Should()
            .Be(ApprovalDecisionStatus.OwnRequest, "nobody approves what was asked for them");
        _hotel.Notify.Alerts.Should().BeEmpty();

        var decided = await approvals.ApproveAsync(id, approver.Id, "fine", Ct);
        await host.WhenResumedAsync();

        decided.Status.Should().Be(ApprovalDecisionStatus.Executed);
        decided.Approval!.Result.Should().Be(AiToolApprovalHandler.QUEUED);
        _hotel.Notify.Alerts.Select(a => a.Player).Should().Equal(bob.Id);
        var told = _told.Single();
        told.Player.Should().Be(staff.Id);
        told.Response.Text.Should().Be("Bob was kicked.");
    }

    [Fact]
    public async Task Approving_returns_before_the_model_answers_and_the_run_goes_on_in_the_background()
    {
        var (staff, approver, bob) = Players();
        var host = _services.GetRequiredService<AiHost>();
        using var live = host.Live(DESK, typeof(KickAgent).Assembly, _services);
        var model = new TaskCompletionSource();
        Reply(new AiToolCallPart("c1", "kick", Json($$"""{"player":{{bob.Id.Value}}}""")));
        // The model's answer after the tool: a slow model, answering only when the test says.
        _model.Enqueue(async () =>
        {
            await model.Task;

            return AiFake.Answer([new AiTextPart("Bob was kicked.")], AiFinish.EndTurn, 10, 5);
        });

        var paused = await host.ForPlugin(DESK)
            .PromptAsync(
                new KickAgent(_hotel, _told),
                "Kick Bob",
                new AiCall { As = AiActor.AsPlayer(staff.Id) },
                Ct
            );
        var decided = await _services
            .GetRequiredService<HotelApprovals>()
            .ApproveAsync(int.Parse(paused.Pending!.ApprovalId), approver.Id, null, Ct)
            .WaitAsync(TimeSpan.FromSeconds(10), Ct);

        decided.Status.Should().Be(ApprovalDecisionStatus.Executed);
        _told.Should().BeEmpty("the model has not answered yet");

        // The active continuation occupies the bounded backlog; another cannot accumulate.
        host.Resume(paused.Pending.ApprovalId, new AiApprovalDecision(true));
        _hostLog
            .AtLeast(LogLevel.Error)
            .Should()
            .ContainSingle(x => x.Message.Contains("queue is full"));

        model.SetResult();
        await host.WhenResumedAsync().WaitAsync(TimeSpan.FromSeconds(10), Ct);

        _told.Single().Response.Text.Should().Be("Bob was kicked.");
        _hotel.Notify.Alerts.Select(a => a.Player).Should().Equal(bob.Id);
    }

    [Fact]
    public async Task A_run_that_fails_going_on_is_told_to_its_agent_logged_as_usage_and_never_runs_the_tool_twice()
    {
        var (staff, approver, bob) = Players();
        var host = _services.GetRequiredService<AiHost>();
        var store = (MemoryAiStore)_services.GetRequiredService<IAiStore>();
        using var live = host.Live(DESK, typeof(BrokenKickAgent).Assembly, _services);
        Reply(new AiToolCallPart("c1", "kick", Json($$"""{"player":{{bob.Id.Value}}}""")));

        var paused = await host.ForPlugin(DESK)
            .PromptAsync(
                new BrokenKickAgent(_hotel, _told),
                "Kick Bob",
                new AiCall { As = AiActor.AsPlayer(staff.Id) },
                Ct
            );
        await _services
            .GetRequiredService<HotelApprovals>()
            .ApproveAsync(int.Parse(paused.Pending!.ApprovalId), approver.Id, null, Ct);
        await host.WhenResumedAsync().WaitAsync(TimeSpan.FromSeconds(10), Ct);

        var told = _told.Single();
        told.Response.Stop.Should().Be(AiStopReason.Failed);
        told.Response.ConversationId.Should().Be(paused.ConversationId);
        store.Usage.Should().ContainSingle(u => u.Outcome == AiProviders.RESUME_FAILED);
        _hotel.Notify.Alerts.Should().ContainSingle("the approved tool ran once");

        // Its conversation no longer waits: going on again does not run the tool a second time.
        (await host.DecidedAsync(paused.Pending.ApprovalId, new AiApprovalDecision(true), Ct))
            .Should()
            .BeNull();
        _hotel.Notify.Alerts.Should().ContainSingle();
    }

    [Fact]
    public async Task A_denied_call_goes_on_without_the_tool_and_the_model_is_told_the_note()
    {
        var (staff, approver, bob) = Players();
        var host = _services.GetRequiredService<AiHost>();
        using var live = host.Live(DESK, typeof(KickAgent).Assembly, _services);
        Reply(new AiToolCallPart("c1", "kick", Json($$"""{"player":{{bob.Id.Value}}}""")));
        Reply(new AiTextPart("I could not kick Bob."));

        var paused = await host.ForPlugin(DESK)
            .PromptAsync(
                new KickAgent(_hotel, _told),
                "Kick Bob",
                new AiCall { As = AiActor.AsPlayer(staff.Id) },
                Ct
            );
        await _services
            .GetRequiredService<HotelApprovals>()
            .DenyAsync(int.Parse(paused.Pending!.ApprovalId), approver.Id, "not today", Ct);
        // The event registry hands the decision to the SDK's listener.
        await new AiApprovalDecidedHandler(host).HandleAsync(
            _events.Of<ApprovalDecidedEvent>().Single(),
            null!,
            Ct
        );
        await host.WhenResumedAsync();

        _hotel.Notify.Alerts.Should().BeEmpty();
        var told = _told.Single();
        told.Response.Text.Should().Be("I could not kick Bob.");
        told.Response.ToolCalls.Single().Result.Should().Contain("not today");
    }

    private (FakePlayer Staff, FakePlayer Approver, FakePlayer Bob) Players()
    {
        var staff = _hotel.Players.Add("Staff");
        var approver = _hotel.Players.Add("Approver");
        staff.Permissions.Add("test.kick");
        approver.Permissions.Add(PermissionNodes.Approvals.DECIDE);

        return (staff, approver, _hotel.Players.Add("Bob", online: true));
    }

    private void Reply(AiPart part) =>
        _model.Enqueue(() =>
            Task.FromResult(
                AiFake.Answer(
                    [part],
                    part is AiToolCallPart ? AiFinish.ToolUse : AiFinish.EndTurn,
                    10,
                    5
                )
            )
        );

    private static System.Text.Json.JsonElement Json(string json) =>
        System.Text.Json.JsonDocument.Parse(json).RootElement.Clone();

    public sealed class KickAgent(IHotel hotel, List<AiResumed> told) : AiAgent
    {
        public override string Instructions => "You kick players when staff ask.";

        public override IEnumerable<IMcpTool> Tools => [new KickTool(hotel)];

        public override Task ResumedAsync(AiResumed resumed, CancellationToken ct)
        {
            told.Add(resumed);

            return Task.CompletedTask;
        }
    }

    /// <summary>Its middleware throws once a tool has answered: the run fails going on.</summary>
    public sealed class BrokenKickAgent(IHotel hotel, List<AiResumed> told) : AiAgent
    {
        public override string Instructions => "You kick players when staff ask.";

        public override IEnumerable<IMcpTool> Tools => [new KickTool(hotel)];

        public override IEnumerable<IAiMiddleware> Middleware => [new Broken()];

        public override Task ResumedAsync(AiResumed resumed, CancellationToken ct)
        {
            told.Add(resumed);

            return Task.CompletedTask;
        }

        private sealed class Broken : IAiMiddleware
        {
            public Task<AiAnswer> HandleAsync(AiStep step, AiNext next, CancellationToken ct) =>
                step.Messages[^1].Parts.OfType<AiToolResultPart>().Any()
                    ? throw new InvalidOperationException("broken")
                    : next(step, ct);
        }
    }

    public sealed record KickInput(int Player);

    [RequiresPermission("test.kick")]
    public sealed class KickTool(IHotel hotel) : McpTool<KickInput>
    {
        public override string Name => "kick";

        public override string Description => "Kicks a player.";

        public override async Task<McpToolResult> HandleAsync(
            KickInput input,
            McpToolContext ctx,
            CancellationToken ct
        )
        {
            await hotel.Notify.AlertAsync(input.Player, "kicked", "You were kicked.", [], ct);

            return McpToolResult.Text("Kicked.");
        }
    }

    private sealed class Script(ConcurrentQueue<Func<Task<AiDriverAnswer>>> answers) : IAiDriver
    {
        public string Name => "script";

        public Task<AiDriverAnswer> SendAsync(
            AiEndpoint endpoint,
            AiProviderRequest request,
            CancellationToken ct
        ) =>
            answers.TryDequeue(out var next)
                ? next()
                : throw new AiProviderException("empty", "no answer scripted");
    }

    private sealed class Permissions(FakeHotel hotel) : IAiPermissions
    {
        public Task<bool> HoldsAsync(
            Turbo.Primitives.Players.PlayerId player,
            string node,
            CancellationToken ct
        ) => hotel.Players.HasPermissionAsync(player, node, ct);
    }

    private sealed class Monitor(AiConfig config) : IOptionsMonitor<AiConfig>
    {
        public AiConfig CurrentValue => config;

        public AiConfig Get(string? name) => config;

        public IDisposable? OnChange(Action<AiConfig, string?> listener) => null;
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
