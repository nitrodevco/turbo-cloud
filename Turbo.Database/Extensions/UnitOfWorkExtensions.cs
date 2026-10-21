using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;

namespace Turbo.Database.Extensions;

/// <summary>What a unit of work answers: its result, and whether what it did is kept.</summary>
public readonly record struct UnitOfWork<T>(T Result, bool Keep);

public static class UnitOfWork
{
    /// <summary>Keep what the work did and answer <paramref name="result"/>.</summary>
    public static UnitOfWork<T> Commit<T>(T result) => new(result, true);

    /// <summary>Undo what the work did and answer <paramref name="result"/>.</summary>
    public static UnitOfWork<T> Rollback<T>(T result) => new(result, false);
}

public static class UnitOfWorkExtensions
{
    /// <summary>
    /// Runs <paramref name="work"/> on a fresh context inside one transaction. What it tracks is
    /// saved and committed together when it answers <see cref="UnitOfWork.Commit{T}"/>; nothing
    /// is kept when it answers <see cref="UnitOfWork.Rollback{T}"/> or throws. Statements it runs
    /// itself (<c>ExecuteUpdate</c>, an early <c>SaveChanges</c>) are part of the same transaction.
    /// </summary>
    public static async Task<T> InTransactionAsync<T>(
        this IDbContextFactory<TurboDbContext> factory,
        Func<TurboDbContext, Task<UnitOfWork<T>>> work,
        CancellationToken ct
    )
    {
        // The work is the caller's: a grain's must run on its activation, so no await here leaves
        // the caller's context.
        var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(true);
        await using var dbScope = db.ConfigureAwait(true);
        var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(true);
        await using var txScope = tx.ConfigureAwait(true);

        var outcome = await work(db).ConfigureAwait(true);

        if (!outcome.Keep)
        {
            await tx.RollbackAsync(ct).ConfigureAwait(true);

            return outcome.Result;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(true);
        await tx.CommitAsync(ct).ConfigureAwait(true);

        return outcome.Result;
    }
}
