using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Ai.Storage;

/// <summary>Conversations and usage in memory, for the test kit (<c>AiFake</c>).</summary>
public sealed class MemoryAiStore : IAiStore
{
    private readonly Lock _lock = new();
    private readonly Dictionary<long, AiConversationRecord> _conversations = [];
    private readonly List<AiUsageRecord> _usage = [];
    private long _next;

    /// <summary>Every usage row, oldest first.</summary>
    public IReadOnlyList<AiUsageRecord> Usage
    {
        get
        {
            lock (_lock)
                return [.. _usage];
        }
    }

    /// <summary>Every conversation kept.</summary>
    public IReadOnlyList<AiConversationRecord> Conversations
    {
        get
        {
            lock (_lock)
                return [.. _conversations.Values.Select(Copy)];
        }
    }

    public Task<AiConversationRecord?> FindAsync(long id, CancellationToken ct)
    {
        lock (_lock)
            return Task.FromResult(
                _conversations.TryGetValue(id, out var found) ? Copy(found) : null
            );
    }

    public Task<AiConversationRecord?> LatestAsync(
        string plugin,
        string agent,
        string participant,
        CancellationToken ct
    )
    {
        lock (_lock)
            return Task.FromResult(
                _conversations
                    .Values.Where(x =>
                        x.Plugin == plugin && x.Agent == agent && x.Participant == participant
                    )
                    .OrderByDescending(x => x.UpdatedAt)
                    .ThenByDescending(x => x.Id)
                    .Select(Copy)
                    .FirstOrDefault()
            );
    }

    public Task<AiConversationRecord?> FindByApprovalAsync(string approvalId, CancellationToken ct)
    {
        lock (_lock)
            return Task.FromResult(
                _conversations
                    .Values.Where(x => x.PendingApprovalId == approvalId)
                    .Select(Copy)
                    .FirstOrDefault()
            );
    }

    public Task SaveAsync(AiConversationRecord conversation, CancellationToken ct)
    {
        lock (_lock)
        {
            if (conversation.Id == 0)
                conversation.Id = ++_next;

            _conversations[conversation.Id] = Copy(conversation);
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        lock (_lock)
            return Task.FromResult(_conversations.Remove(id));
    }

    public Task RecordUsageAsync(AiUsageRecord usage, CancellationToken ct)
    {
        lock (_lock)
            _usage.Add(usage);

        return Task.CompletedTask;
    }

    public Task<long> TokensSinceAsync(int playerId, DateTime since, CancellationToken ct)
    {
        lock (_lock)
            return Task.FromResult(
                _usage
                    .Where(x => x.PlayerId == playerId && x.At >= since)
                    .Sum(x => (long)x.InputTokens + x.OutputTokens)
            );
    }

    public Task<IReadOnlyList<AiUsageTotal>> TotalsSinceAsync(DateTime since, CancellationToken ct)
    {
        lock (_lock)
            return Task.FromResult<IReadOnlyList<AiUsageTotal>>(
                [
                    .. _usage
                        .Where(x => x.At >= since)
                        .GroupBy(x => (x.Plugin, x.Provider, x.Model))
                        .Select(g => new AiUsageTotal(
                            g.Key.Plugin,
                            g.Key.Provider,
                            g.Key.Model,
                            g.Count(),
                            g.Count(x => x.Outcome != "ok"),
                            g.Sum(x => (long)x.InputTokens),
                            g.Sum(x => (long)x.OutputTokens),
                            g.Sum(x => x.Cost)
                        )),
                ]
            );
    }

    private static AiConversationRecord Copy(AiConversationRecord x) =>
        new()
        {
            Id = x.Id,
            Plugin = x.Plugin,
            Agent = x.Agent,
            AgentType = x.AgentType,
            Participant = x.Participant,
            Messages = x.Messages,
            PendingApprovalId = x.PendingApprovalId,
            Pending = x.Pending,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
        };
}
