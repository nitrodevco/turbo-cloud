using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Contracts.Approvals;

/// <summary>
/// Human approval (<c>docs/approvals.md</c>): an action that must not change the hotel until a
/// person says yes. A plugin, an AI agent, an MCP tool or core asks with <see cref="RequestAsync"/>;
/// the request waits, saved, until a staff member holding its node approves or denies it, or it
/// expires. On approval the <see cref="ApprovalHandler{TArgs}"/> of its kind carries it out, once,
/// however many people press approve. Requests survive a restart. A plugin is given one bound to
/// it: what it asks is recorded as its own.
/// </summary>
public interface IApprovals
{
    /// <summary>
    /// Saves a request and answers it, pending. Throws <see cref="ArgumentException"/> for a
    /// request that can't be saved (a kind with other characters, no summary, arguments that are
    /// not an object, an expiry out of range).
    /// </summary>
    Task<Approval> RequestAsync(ApprovalRequest request, CancellationToken ct);

    /// <summary>The request, null when there is none with that id. One past its expiry reads expired.</summary>
    Task<Approval?> GetAsync(int id, CancellationToken ct);

    /// <summary>Requests newest first, narrowed by <paramref name="query"/>.</summary>
    Task<ApprovalList> ListAsync(ApprovalQuery query, CancellationToken ct);

    /// <summary>How many requests wait for a decision.</summary>
    Task<int> CountPendingAsync(CancellationToken ct);

    /// <summary>
    /// Approves the request as <paramref name="by"/> and carries it out. Refused when it is not
    /// pending (decided already, by someone else too), expired, asked for by or for
    /// <paramref name="by"/>, when <paramref name="by"/> does not hold its node, or when nothing
    /// live can carry out its kind (it stays pending). It runs at most once.
    /// </summary>
    Task<ApprovalDecision> ApproveAsync(int id, PlayerId by, string? note, CancellationToken ct);

    /// <summary>Denies the request as <paramref name="by"/>; refused as <see cref="ApproveAsync"/> is.</summary>
    Task<ApprovalDecision> DenyAsync(int id, PlayerId by, string? note, CancellationToken ct);
}

/// <summary>
/// What is asked for. <see cref="Kind"/> names the <see cref="ApprovalHandler{TArgs}"/> that
/// carries it out; <see cref="Arguments"/> is what it is given (a record, saved as JSON);
/// <see cref="Summary"/> is the sentence the approver reads.
/// </summary>
public sealed record ApprovalRequest
{
    /// <summary>The longest <see cref="Kind"/>.</summary>
    public const int KIND_MAX_LENGTH = 64;

    /// <summary>The longest <see cref="Summary"/>.</summary>
    public const int SUMMARY_MAX_LENGTH = 500;

    /// <summary>The most the arguments may take as JSON.</summary>
    public const int ARGUMENTS_MAX_LENGTH = 16_000;

    /// <summary>How long a request waits when <see cref="ExpiresIn"/> is not given.</summary>
    public static readonly TimeSpan DEFAULT_EXPIRY = TimeSpan.FromHours(24);

    /// <summary>The longest a request may wait.</summary>
    public static readonly TimeSpan MAX_EXPIRY = TimeSpan.FromDays(30);

    /// <summary>
    /// Which handler carries it out: lower-case letters, digits, <c>.</c>, <c>_</c>, <c>-</c> and
    /// <c>:</c> (<c>refund.credits</c>, <c>ai.tool</c>).
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>What will happen, in a sentence the approver reads ("Refund Alice 5,000 credits").</summary>
    public required string Summary { get; init; }

    /// <summary>
    /// What the handler is given: a record or class (written in camelCase), or a JSON object.
    /// The approver sees it as it is saved.
    /// </summary>
    public object? Arguments { get; init; }

    /// <summary>Who or what asks.</summary>
    public required ApprovalRequester Requester { get; init; }

    /// <summary>The permission node an approver must hold; <see cref="PermissionNodes.Approvals.DECIDE"/> by default.</summary>
    public string Node { get; init; } = PermissionNodes.Approvals.DECIDE;

    /// <summary>How long it waits for a decision before it expires; 24 hours by default, at most 30 days.</summary>
    public TimeSpan? ExpiresIn { get; init; }
}

/// <summary>
/// Who or what asked: its kind, the player it acts for (a staff member, the owner of an MCP token
/// or of an AI agent's run), and a name for one that is not a player (the agent, the token).
/// The player it acts for can never approve it.
/// </summary>
public sealed record ApprovalRequester(
    ApprovalRequesterKind Kind,
    PlayerId? OnBehalfOf = null,
    string? Name = null
)
{
    /// <summary>The longest <see cref="Name"/>.</summary>
    public const int NAME_MAX_LENGTH = 64;

    public static ApprovalRequester Player(PlayerId player) =>
        new(ApprovalRequesterKind.Player, player);

    public static ApprovalRequester Staff(PlayerId staff) =>
        new(ApprovalRequesterKind.Staff, staff);

    /// <summary>An AI agent, running for <paramref name="onBehalfOf"/> when a player started it.</summary>
    public static ApprovalRequester Agent(string name, PlayerId? onBehalfOf = null) =>
        new(ApprovalRequesterKind.Agent, onBehalfOf, name);

    /// <summary>An assistant using a staff member's access token (MCP).</summary>
    public static ApprovalRequester Token(PlayerId owner, string? name = null) =>
        new(ApprovalRequesterKind.Token, owner, name);

    public static ApprovalRequester System(string name) =>
        new(ApprovalRequesterKind.System, null, name);
}

/// <summary>What kind of thing asked.</summary>
public enum ApprovalRequesterKind
{
    Player,
    Staff,
    Agent,
    Token,
    Plugin,
    System,
}

/// <summary>Where a request stands.</summary>
public enum ApprovalState
{
    /// <summary>Waiting for a decision.</summary>
    Pending,

    /// <summary>Approved and being carried out, or approved on a server that stopped before it ran.</summary>
    Approved,

    /// <summary>Approved and carried out.</summary>
    Executed,

    /// <summary>Approved, and carrying it out failed; <see cref="Approval.Result"/> says why.</summary>
    Failed,

    Denied,

    /// <summary>Nobody decided in time.</summary>
    Expired,
}

/// <summary>A request as saved: what was asked, by whom, and what became of it.</summary>
public sealed record Approval
{
    public required int Id { get; init; }

    public required string Kind { get; init; }

    /// <summary>The plugin that asked (its key); null for core.</summary>
    public string? Plugin { get; init; }

    public required string Summary { get; init; }

    /// <summary>The arguments as saved: a JSON object.</summary>
    public required JsonElement Arguments { get; init; }

    public required ApprovalRequester Requester { get; init; }

    public required string Node { get; init; }

    public required ApprovalState State { get; init; }

    public required DateTime RequestedAtUtc { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }

    /// <summary>Who approved or denied it; null while pending and when it expired.</summary>
    public PlayerId? DecidedBy { get; init; }

    /// <summary>When it was approved, denied or expired.</summary>
    public DateTime? DecidedAtUtc { get; init; }

    /// <summary>What the approver wrote with the decision.</summary>
    public string? Note { get; init; }

    public DateTime? ExecutedAtUtc { get; init; }

    /// <summary>What carrying it out answered, or why it failed.</summary>
    public string? Result { get; init; }

    /// <summary>The arguments read as <typeparamref name="T"/> (camelCase).</summary>
    public T ArgumentsAs<T>() =>
        Arguments.Deserialize<T>(ApprovalJson.Options)
        ?? throw new InvalidOperationException($"The approval {Id}'s arguments are empty.");
}

/// <summary>Which requests to list. Null narrows nothing.</summary>
public sealed record ApprovalQuery
{
    public ApprovalState? State { get; init; }

    public string? Kind { get; init; }

    /// <summary>A plugin's key; <c>core</c> for core's own.</summary>
    public string? Plugin { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

/// <summary>A page of requests, newest first, and how many match in all.</summary>
public sealed record ApprovalList(int Total, ImmutableArray<Approval> Entries);

/// <summary>How a decision went.</summary>
public enum ApprovalDecisionStatus
{
    /// <summary>Approved and carried out.</summary>
    Executed,

    /// <summary>Approved, and carrying it out failed: <see cref="ApprovalDecision.Message"/> says why.</summary>
    Failed,

    Denied,

    NotFound,

    /// <summary>Someone decided it already, or it expired.</summary>
    NotPending,

    /// <summary>The decider is who asked, or who it was asked for.</summary>
    OwnRequest,

    /// <summary>The decider does not hold its node.</summary>
    NotPermitted,

    /// <summary>Nothing live carries out its kind (its plugin is not loaded); it stays pending.</summary>
    NoHandler,
}

/// <summary>A decision's outcome, a sentence for the decider, and the request as it now stands.</summary>
public sealed record ApprovalDecision(
    ApprovalDecisionStatus Status,
    string Message,
    Approval? Approval = null
)
{
    /// <summary>It was approved (carried out or failed) or denied.</summary>
    public bool Decided =>
        Status
            is ApprovalDecisionStatus.Executed
                or ApprovalDecisionStatus.Failed
                or ApprovalDecisionStatus.Denied;
}

/// <summary>How arguments are written and read: camelCase, enums by name.</summary>
public static class ApprovalJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        json.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        );
        json.MakeReadOnly(populateMissingResolver: true);

        return json;
    }
}
