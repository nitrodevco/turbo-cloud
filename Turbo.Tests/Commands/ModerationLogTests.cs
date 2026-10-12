using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Operations;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>
/// The moderation history's writer. A record that cannot be written must not undo or fail the
/// action it records (a ban stands when the history cannot be written), and what it would have
/// recorded goes to the server log, so it is not lost outright.
/// </summary>
public sealed class ModerationLogTests
{
    [Fact]
    public async Task A_record_the_database_refuses_is_logged_and_does_not_throw()
    {
        var log = new CapturingLogger<IModerationLog>();
        var moderation = new ModerationLog(new UnreachableDb(), log);

        var record = () =>
            moderation.RecordAsync(
                new ModerationRecord
                {
                    Action = ModerationActionType.Ban,
                    Source = ModerationSource.Console,
                    TargetPlayerId = 5,
                    Reason = "spam",
                },
                TestContext.Current.CancellationToken
            );

        await record.Should().NotThrowAsync();
        log.AtLeast(LogLevel.Error)
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Contain("Ban")
            .And.Contain("spam");
    }

    [Fact]
    public async Task A_reason_too_long_for_its_column_is_cut_to_fit_rather_than_lost()
    {
        using var db = new SqliteDb();
        var moderation = new ModerationLog(db, new CapturingLogger<IModerationLog>());

        await moderation.RecordAsync(
            new ModerationRecord
            {
                Action = ModerationActionType.Warn,
                Source = ModerationSource.Panel,
                TargetPlayerId = 5,
                Reason = new string('r', 300),
                Detail = new string('d', 600),
            },
            TestContext.Current.CancellationToken
        );

        await using var read = db.CreateDbContext();
        var row = await read.ModerationActions.SingleAsync(TestContext.Current.CancellationToken);
        (row.Reason!.Length, row.Detail!.Length).Should().Be((255, 500));
    }

    private sealed class UnreachableDb : IDbContextFactory<TurboDbContext>
    {
        public TurboDbContext CreateDbContext() =>
            throw new InvalidOperationException("the database is down");
    }
}
