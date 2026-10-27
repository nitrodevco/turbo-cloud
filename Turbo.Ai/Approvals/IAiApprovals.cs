using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Ai.Approvals;

/// <summary>
/// Where an agent's tool that changes things asks a staff member first (<c>docs/ai-sdk.md</c>,
/// Approvals). The hotel's approvals answer it; until a hotel has them, <see cref="NoApprovals"/>
/// refuses such tools, so an agent never runs one unasked. When an approval is decided, its
/// adapter calls <see cref="AiHost.DecidedAsync"/>, and the run that waited goes on.
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
/// What a staff member is asked: the plugin, agent and tool, a title and the call's arguments in
/// words, the nodes they must hold (the tool's), who the agent acts for, and the conversation it
/// resumes.
/// </summary>
public sealed record AiApprovalRequest(
    string Plugin,
    string Agent,
    string Tool,
    string Title,
    string Detail,
    ImmutableArray<string> Nodes,
    PlayerId? RequestedFor,
    long ConversationId
);

/// <summary>A decision: approved or not, by whom, and why.</summary>
public sealed record AiApprovalDecision(
    bool Approved,
    PlayerId? DecidedBy = null,
    string? Note = null
);

/// <summary>A hotel without approvals: a tool that would need one is never run by an agent.</summary>
public sealed class NoApprovals : IAiApprovals
{
    public bool Enabled => false;

    public Task<string> RequestAsync(AiApprovalRequest request, CancellationToken ct) =>
        throw new System.InvalidOperationException("This hotel has no approvals.");

    public Task<AiApprovalDecision?> GetAsync(string id, CancellationToken ct) =>
        Task.FromResult<AiApprovalDecision?>(null);
}
