using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Permissions;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Players.Permissions;

/// <summary>
/// The one way both permission grains set and remove an assignment or membership row, so a
/// group's rows and a player's cannot drift apart. <c>find</c> names the one row (owner, node or
/// key, and whether temporary), and stays a query so it runs in SQL. Nothing is saved here: the
/// grain adds its audit row and saves both together.
/// </summary>
internal static class PermissionRowWrites
{
    /// <summary>
    /// Sets a node or meta value: adds the row, or replaces the value and expiry of the one
    /// <paramref name="find"/> names. The expiry is resolved against the row already there
    /// (<see cref="PermissionExpiry.Resolve"/>). <c>Unchanged</c> when it already said so.
    /// </summary>
    public static Task<(PermissionChangeResultType Result, TRow Row)> SetAsync<TRow, TValue>(
        DbSet<TRow> rows,
        Expression<Func<TRow, bool>> find,
        Func<DateTime?, TRow> create,
        TValue value,
        DateTime? expiresAt,
        PermissionExpiryModeType mode,
        DateTime now,
        CancellationToken ct
    )
        where TRow : class, IPermissionAssignmentEntity<TValue> =>
        UpsertAsync(
            rows,
            find,
            create,
            row => EqualityComparer<TValue>.Default.Equals(row.Value, value),
            row => row.Value = value,
            expiresAt,
            mode,
            now,
            ct
        );

    /// <summary>Adds a membership, or changes the expiry of the one <paramref name="find"/> names.</summary>
    public static Task<(PermissionChangeResultType Result, TRow Row)> SetExpiryAsync<TRow>(
        DbSet<TRow> rows,
        Expression<Func<TRow, bool>> find,
        Func<DateTime?, TRow> create,
        DateTime? expiresAt,
        PermissionExpiryModeType mode,
        DateTime now,
        CancellationToken ct
    )
        where TRow : class, IPermissionExpiringEntity =>
        UpsertAsync(
            rows,
            find,
            create,
            static _ => true,
            static _ => { },
            expiresAt,
            mode,
            now,
            ct
        );

    /// <summary>Removes the row <paramref name="find"/> names, and returns it; <c>null</c> when there was none.</summary>
    public static async Task<TRow?> RemoveAsync<TRow>(
        DbSet<TRow> rows,
        Expression<Func<TRow, bool>> find,
        CancellationToken ct
    )
        where TRow : class
    {
        var row = await rows.FirstOrDefaultAsync(find, ct);

        if (row is not null)
            rows.Remove(row);

        return row;
    }

    private static async Task<(PermissionChangeResultType Result, TRow Row)> UpsertAsync<TRow>(
        DbSet<TRow> rows,
        Expression<Func<TRow, bool>> find,
        Func<DateTime?, TRow> create,
        Func<TRow, bool> holdsValue,
        Action<TRow> setValue,
        DateTime? expiresAt,
        PermissionExpiryModeType mode,
        DateTime now,
        CancellationToken ct
    )
        where TRow : class, IPermissionExpiringEntity
    {
        var row = await rows.FirstOrDefaultAsync(find, ct);
        var until = PermissionExpiry.Resolve(expiresAt, row?.ExpiresAt, mode, now);

        if (row is null)
        {
            row = create(until);
            rows.Add(row);

            return (PermissionChangeResultType.Changed, row);
        }

        if (holdsValue(row) && row.ExpiresAt == until)
            return (PermissionChangeResultType.Unchanged, row);

        setValue(row);
        row.ExpiresAt = until;

        return (PermissionChangeResultType.Changed, row);
    }
}
