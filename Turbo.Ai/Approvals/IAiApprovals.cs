using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Approvals;
using Turbo.Events.Registry;
using Turbo.Plugins.Approvals;
using Turbo.Primitives.Players;

namespace Turbo.Ai.Approvals;

/// <summary>
/// Where an agent's tool that changes things asks a staff member first (<c>docs/ai-sdk.md</c>,
/// Approvals): the hotel's approvals (<see cref="HotelAiApprovals"/>, <c>docs/approvals.md</c>).
/// When one is decided, <see cref="AiHost.DecidedAsync"/> is called and the run that waited goes
/// on. A host without approvals has <see cref="NoApprovals"/>: such tools are never run.
/// </summary>
public interface IAiApprovals
{
    /// <summary>Whether approvals can be asked for at all.</summary>
    bool Enabled { get; }

    /// <summary>Asks for one; answers its id, by which <see cref="AiHost.DecidedAsync"/> is called.</summary>
    Task<string> RequestAsync(AiApprovalRequest request, CancellationToken ct);

    /// <summary>The decision, or null while it waits (or is not known).</summary>
    Task<AiApprovalDecision?> GetAsync(string id, CancellationToken ct);
}

/// <summary>
/// What a staff member is asked: the plugin, agent and tool, the sentence they read, the call's
/// checked arguments, the node they must hold (the tool's <c>ApprovalNode</c>), who the agent acts
/// for, and the conversation it resumes.
/// </summary>
public sealed record AiApprovalRequest(
    string Plugin,
    string Agent,
    string Tool,
    string Summary,
    JsonElement Arguments,
    string Node,
    PlayerId? RequestedFor,
    long ConversationId
);

/// <summary>A decision: approved or not, by whom, and why.</summary>
public sealed record AiApprovalDecision(
    bool Approved,
    PlayerId? DecidedBy = null,
    string? Note = null
);

/// <summary>
/// The arguments of an approval an agent's tool call asked for: the conversation whose run waits,
/// the tool and its arguments. Its kind is <see cref="KIND"/>.
/// </summary>
public sealed record AiToolApproval(long ConversationId, string Tool, JsonElement Arguments)
{
    public const string KIND = "ai.tool";
}

/// <summary>A host without approvals: a tool that would need one is never run by an agent.</summary>
public sealed class NoApprovals : IAiApprovals
{
    public bool Enabled => false;

    public Task<string> RequestAsync(AiApprovalRequest request, CancellationToken ct) =>
        throw new InvalidOperationException("This hotel has no approvals.");

    public Task<AiApprovalDecision?> GetAsync(string id, CancellationToken ct) =>
        Task.FromResult<AiApprovalDecision?>(null);
}

/// <summary>
/// The hotel's approvals (<c>docs/approvals.md</c>) for agents: a request of kind
/// <see cref="AiToolApproval.KIND"/>, recorded as its plugin's, asked by the agent for the player it
/// acts for (who can't approve it). Approved, <see cref="AiToolApprovalHandler"/> resumes the run;
/// denied or expired, <see cref="AiApprovalDecidedHandler"/> does.
/// </summary>
internal sealed class HotelAiApprovals(IServiceProvider services) : IAiApprovals
{
    public bool Enabled => services.GetService<HotelApprovals>() is not null;

    public async Task<string> RequestAsync(AiApprovalRequest request, CancellationToken ct)
    {
        var approval = await services
            .GetRequiredService<HotelApprovals>()
            .RequestAsync(
                new ApprovalRequest
                {
                    Kind = AiToolApproval.KIND,
                    Summary = Cut(request.Summary, ApprovalRequest.SUMMARY_MAX_LENGTH),
                    Arguments = new AiToolApproval(
                        request.ConversationId,
                        request.Tool,
                        request.Arguments
                    ),
                    Requester = ApprovalRequester.Agent(
                        Cut(request.Agent, ApprovalRequester.NAME_MAX_LENGTH),
                        request.RequestedFor
                    ),
                    Node = request.Node,
                },
                request.Plugin,
                ct
            )
            .ConfigureAwait(false);

        return approval.Id.ToString(CultureInfo.InvariantCulture);
    }

    public async Task<AiApprovalDecision?> GetAsync(string id, CancellationToken ct)
    {
        if (
            !int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || services.GetService<HotelApprovals>() is not { } approvals
            || await approvals.GetAsync(number, ct).ConfigureAwait(false) is not { } approval
        )
            return null;

        return Decision(approval);
    }

    /// <summary>The decision an approval holds; null while it is pending.</summary>
    internal static AiApprovalDecision? Decision(Approval approval) =>
        approval.State switch
        {
            ApprovalState.Pending => null,
            ApprovalState.Denied or ApprovalState.Expired => new AiApprovalDecision(
                false,
                approval.DecidedBy,
                approval.State == ApprovalState.Expired ? "nobody decided in time" : approval.Note
            ),
            _ => new AiApprovalDecision(true, approval.DecidedBy, approval.Note),
        };

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..max];
}

/// <summary>Carries out an approved agent's tool call: the run that waited goes on, and its answer is the approval's result.</summary>
internal sealed class AiToolApprovalHandler(AiHost host) : ApprovalHandler<AiToolApproval>
{
    public override string Kind => AiToolApproval.KIND;

    public override async Task<ApprovalResult> ExecuteAsync(
        AiToolApproval arguments,
        Approval approval,
        CancellationToken ct
    )
    {
        var resumed = await host.DecidedAsync(
                approval.Id.ToString(CultureInfo.InvariantCulture),
                new AiApprovalDecision(true, approval.DecidedBy, approval.Note),
                ct
            )
            .ConfigureAwait(false);

        return resumed is null
            ? ApprovalResult.Failed(
                "No agent's run went on: it waits on no approval any more, or its plugin is not running (it goes on when the plugin resumes it)."
            )
            : ApprovalResult.Done(
                $"{resumed.Stop}: {Cut(resumed.Text.Length > 0 ? resumed.Text : resumed.Message ?? "", ApprovalResult.MESSAGE_MAX_LENGTH - 40)}"
            );
    }

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..max];
}

/// <summary>An agent's tool call was denied, or nobody decided in time: the run that waited goes on without it.</summary>
public sealed class AiApprovalDecidedHandler(AiHost host) : IEventHandler<ApprovalDecidedEvent>
{
    public async ValueTask HandleAsync(
        ApprovalDecidedEvent env,
        EventContext ctx,
        CancellationToken ct
    )
    {
        if (
            env.Approval.Kind != AiToolApproval.KIND
            || HotelAiApprovals.Decision(env.Approval) is not { Approved: false } decision
        )
            return;

        await host.DecidedAsync(
                env.Approval.Id.ToString(CultureInfo.InvariantCulture),
                decision,
                ct
            )
            .ConfigureAwait(false);
    }
}
