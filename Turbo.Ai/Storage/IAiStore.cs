using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Ai.Storage;

/// <summary>Where conversations and the usage log are kept: the database on a server, memory in a test.</summary>
public interface IAiStore
{
    Task<AiConversationRecord?> FindAsync(long id, CancellationToken ct);

    /// <summary>The participant's latest conversation with the plugin's agent.</summary>
    Task<AiConversationRecord?> LatestAsync(
        string plugin,
        string agent,
        string participant,
        CancellationToken ct
    );

    Task<AiConversationRecord?> FindByApprovalAsync(string approvalId, CancellationToken ct);

    /// <summary>Inserts it (its id 0, set on return) or updates it.</summary>
    Task SaveAsync(AiConversationRecord conversation, CancellationToken ct);

    Task<bool> DeleteAsync(long id, CancellationToken ct);

    Task RecordUsageAsync(AiUsageRecord usage, CancellationToken ct);

    /// <summary>Tokens (in and out) the player's calls used since <paramref name="since"/>.</summary>
    Task<long> TokensSinceAsync(int playerId, DateTime since, CancellationToken ct);

    /// <summary>Usage since <paramref name="since"/> by UTC day, plugin, provider and model, for the admin panel.</summary>
    Task<IReadOnlyList<AiUsageTotal>> TotalsSinceAsync(DateTime since, CancellationToken ct);
}

/// <summary>A conversation as kept (<c>ai_conversations</c>).</summary>
public sealed class AiConversationRecord
{
    public long Id { get; set; }

    public required string Plugin { get; init; }

    public required string Agent { get; init; }

    public string? AgentType { get; set; }

    public required string Participant { get; init; }

    public required string Messages { get; set; }

    public string? PendingApprovalId { get; set; }

    public string? Pending { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

/// <summary>One model answer, or a failed attempt, as the usage log keeps it (<c>ai_usage</c>).</summary>
public sealed record AiUsageRecord(
    string Plugin,
    string Agent,
    string Provider,
    string Model,
    int? PlayerId,
    long? ConversationId,
    int InputTokens,
    int OutputTokens,
    decimal Cost,
    int DurationMs,
    string Outcome,
    DateTime At
);

/// <summary>Usage summed by UTC day, plugin, provider and model.</summary>
public sealed record AiUsageTotal(
    DateTime Day,
    string Plugin,
    string Provider,
    string Model,
    int Calls,
    int Failed,
    long InputTokens,
    long OutputTokens,
    decimal Cost
);
