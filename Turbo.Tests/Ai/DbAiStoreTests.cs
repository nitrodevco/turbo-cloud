using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Turbo.Ai.Storage;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Ai;

/// <summary>
/// The SDK's tables (<c>ai_conversations</c>, <c>ai_usage</c>) through the database store, on a
/// relational database: a conversation saved and found again (latest, by its approval), a
/// player's tokens since a day, and the usage summed for the panel.
/// </summary>
public sealed class DbAiStoreTests : IDisposable
{
    private static readonly DateTime NOW = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();
    private readonly DbAiStore _store;

    public DbAiStoreTests() => _store = new DbAiStore(_db);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_conversation_is_found_as_the_latest_and_by_its_approval_after_an_update()
    {
        var older = Conversation(NOW.AddHours(-1));
        var newer = Conversation(NOW);

        await _store.SaveAsync(older, CancellationToken.None);
        await _store.SaveAsync(newer, CancellationToken.None);
        newer.PendingApprovalId = "approval-1";
        newer.Messages = "[{\"role\":\"User\",\"parts\":[]}]";
        await _store.SaveAsync(newer, CancellationToken.None);

        (await _store.LatestAsync("desk", "Helper", "player:5", CancellationToken.None))!.Id.Should().Be(newer.Id);
        var waiting = await _store.FindByApprovalAsync("approval-1", CancellationToken.None);
        waiting!.Messages.Should().Be(newer.Messages);
        (await _store.DeleteAsync(older.Id, CancellationToken.None)).Should().BeTrue();
        (await _store.FindAsync(older.Id, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Usage_counts_a_players_tokens_since_a_day_and_sums_by_plugin_provider_and_model()
    {
        await _store.RecordUsageAsync(Usage("claude", 100, 20, "ok", NOW.AddDays(-1)), CancellationToken.None);
        await _store.RecordUsageAsync(Usage("claude", 50, 10, "ok", NOW), CancellationToken.None);
        await _store.RecordUsageAsync(Usage("claude", 0, 0, "http_529", NOW), CancellationToken.None);
        await _store.RecordUsageAsync(Usage("local", 7, 3, "ok", NOW), CancellationToken.None);

        (await _store.TokensSinceAsync(5, NOW.Date, CancellationToken.None)).Should().Be(70);
        (await _store.TotalsSinceAsync(NOW.Date, CancellationToken.None))
            .Should().Equal(
                new AiUsageTotal("desk", "claude", "m", 2, 1, 50, 10, 0.5m),
                new AiUsageTotal("desk", "local", "m", 1, 0, 7, 3, 0.25m)
            );
    }

    private static AiConversationRecord Conversation(DateTime at) =>
        new()
        {
            Plugin = "desk",
            Agent = "Helper",
            AgentType = "Desk.Helper",
            Participant = "player:5",
            Messages = "[]",
            CreatedAt = at,
            UpdatedAt = at,
        };

    private static AiUsageRecord Usage(string provider, int input, int output, string outcome, DateTime at) =>
        new("desk", "Helper", provider, "m", 5, null, input, output, 0.25m, 12, outcome, at);
}
