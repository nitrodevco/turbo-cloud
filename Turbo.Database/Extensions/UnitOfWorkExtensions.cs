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
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var outcome = await work(db);

        if (!outcome.Keep)
        {
            await tx.RollbackAsync(ct);

            return outcome.Result;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return outcome.Result;
    }
}
