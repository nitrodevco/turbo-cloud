using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Ai;

namespace Turbo.Ai.Storage;

/// <summary>Conversations and usage in the hotel's database (<c>ai_conversations</c>, <c>ai_usage</c>).</summary>
internal sealed class DbAiStore(IDbContextFactory<TurboDbContext> db) : IAiStore
{
    public async Task<AiConversationRecord?> FindAsync(long id, CancellationToken ct)
    {
        await using var ctx = await db.CreateDbContextAsync(ct).ConfigureAwait(false);

        return From(
            await ctx
                .AiConversations.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct)
                .ConfigureAwait(false)
        );
    }

    public async Task<AiConversationRecord?> LatestAsync(
        string plugin,
        string agent,
        string participant,
        CancellationToken ct
    )
    {
        await using var ctx = await db.CreateDbContextAsync(ct).ConfigureAwait(false);

        return From(
            await ctx
                .AiConversations.AsNoTracking()
                .Where(x =>
                    x.PluginKey == plugin && x.Agent == agent && x.Participant == participant
                )
                .OrderByDescending(x => x.UpdatedAt)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false)
        );
    }

    public async Task<AiConversationRecord?> FindByApprovalAsync(
        string approvalId,
        CancellationToken ct
    )
    {
        await using var ctx = await db.CreateDbContextAsync(ct).ConfigureAwait(false);

        return From(
            await ctx
                .AiConversations.AsNoTracking()
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(x => x.PendingApprovalId == approvalId, ct)
                .ConfigureAwait(false)
        );
    }

    public async Task SaveAsync(AiConversationRecord conversation, CancellationToken ct)
    {
        await using var ctx = await db.CreateDbContextAsync(ct).ConfigureAwait(false);

        if (conversation.Id == 0)
        {
            var row = new AiConversationEntity
            {
                PluginKey = conversation.Plugin,
                Agent = conversation.Agent,
                AgentType = conversation.AgentType,
                Participant = conversation.Participant,
                Messages = conversation.Messages,
                PendingApprovalId = conversation.PendingApprovalId,
                Pending = conversation.Pending,
                CreatedAt = conversation.CreatedAt,
                UpdatedAt = conversation.UpdatedAt,
            };

            ctx.AiConversations.Add(row);
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
            conversation.Id = row.Id;

            return;
        }

        await ctx
            .AiConversations.Where(x => x.Id == conversation.Id)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Messages, conversation.Messages)
                        .SetProperty(x => x.AgentType, conversation.AgentType)
                        .SetProperty(x => x.PendingApprovalId, conversation.PendingApprovalId)
                        .SetProperty(x => x.Pending, conversation.Pending)
                        .SetProperty(x => x.UpdatedAt, conversation.UpdatedAt),
                ct
            )
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        await using var ctx = await db.CreateDbContextAsync(ct).ConfigureAwait(false);

        return await ctx
                .AiConversations.Where(x => x.Id == id)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false) > 0;
    }

    public async Task RecordUsageAsync(AiUsageRecord usage, CancellationToken ct)
    {
        await using var ctx = await db.CreateDbContextAsync(ct).ConfigureAwait(false);

        ctx.AiUsage.Add(
            new AiUsageEntity
            {
                PluginKey = usage.Plugin,
                Agent = Cut(usage.Agent, AiConversationEntity.AGENT_MAX_LENGTH),
                Provider = Cut(usage.Provider, 64),
                Model = Cut(usage.Model, 128),
                PlayerId = usage.PlayerId,
                ConversationId = usage.ConversationId,
                InputTokens = usage.InputTokens,
                OutputTokens = usage.OutputTokens,
                Cost = usage.Cost,
                DurationMs = usage.DurationMs,
                Outcome = Cut(usage.Outcome, 32),
                CreatedAt = usage.At,
            }
        );
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<long> TokensSinceAsync(int playerId, DateTime since, CancellationToken ct)
    {
        await using var ctx = await db.CreateDbContextAsync(ct).ConfigureAwait(false);

        return await ctx
            .AiUsage.Where(x => x.PlayerId == playerId && x.CreatedAt >= since)
            .SumAsync(x => (long)x.InputTokens + x.OutputTokens, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AiUsageTotal>> TotalsSinceAsync(
        DateTime since,
        CancellationToken ct
    )
    {
        await using var ctx = await db.CreateDbContextAsync(ct).ConfigureAwait(false);

        var totals = await ctx
            .AiUsage.Where(x => x.CreatedAt >= since)
            .GroupBy(x => new
            {
                x.PluginKey,
                x.Provider,
                x.Model,
            })
            .OrderBy(g => g.Key.PluginKey)
            .ThenBy(g => g.Key.Provider)
            .ThenBy(g => g.Key.Model)
            .Select(g => new
            {
                g.Key.PluginKey,
                g.Key.Provider,
                g.Key.Model,
                Calls = g.Count(),
                Failed = g.Count(x => x.Outcome != "ok"),
                Input = g.Sum(x => (long)x.InputTokens),
                Output = g.Sum(x => (long)x.OutputTokens),
                // As a double in the query: not every database sums decimals (SQLite).
                Cost = g.Sum(x => (double)x.Cost),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return
        [
            .. totals.Select(x => new AiUsageTotal(
                x.PluginKey,
                x.Provider,
                x.Model,
                x.Calls,
                x.Failed,
                x.Input,
                x.Output,
                Math.Round((decimal)x.Cost, 6)
            )),
        ];
    }

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..max];

    private static AiConversationRecord? From(AiConversationEntity? x) =>
        x is null
            ? null
            : new AiConversationRecord
            {
                Id = x.Id,
                Plugin = x.PluginKey,
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
