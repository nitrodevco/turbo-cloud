using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Backups;
using Turbo.Admin.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// Backing up the database from the panel, for staff with <c>admin.database.backup</c>: the
/// tables to choose from, a backup of those chosen built as SQL (as a job: a large database
/// outlasts the panel's proxy), the backups kept to download, the file itself, and deleting one.
/// Every backup built, downloaded or deleted is in the command log
/// (<see cref="AdminActionLog.DATABASE"/>).
/// </summary>
internal sealed class DatabaseEndpoints(
    AdminDatabaseBackups backups,
    AdminJobs jobs,
    AdminActionLog audit
)
{
    private const string NO_ACCESS = "You can't back up the database.";
    private const string GONE =
        "That backup is gone: a backup is kept for a while after it is built, then deleted. Build it again.";
    private const string TITLE = "The database backup";

    public void Map(RouteGroupBuilder secured)
    {
        // One node for all of it: seeing the tables is the start of a backup, and a backup is
        // what building, downloading and deleting one are about.
        var group = secured
            .MapGroup("/database")
            .RequirePermission(PermissionNodes.Admin.DATABASE_BACKUP, NO_ACCESS)
            .RequirePermissionToChange(PermissionNodes.Admin.DATABASE_BACKUP, NO_ACCESS);

        group.MapGet("/tables", TablesAsync);
        group.MapGet("/backups", List);
        group.MapPost("/backups", BuildAsync);
        group.MapGet("/backups/{id:guid}/file", DownloadAsync);
        group.MapDelete("/backups/{id:guid}", DeleteAsync);
    }

    private async Task<IResult> TablesAsync(CancellationToken ct) =>
        Results.Ok(
            new DatabaseTablesResponse([
                .. (await backups.TablesAsync(ct).ConfigureAwait(false)).Select(
                    x => new DatabaseTableItem(x.Name, x.Rows, x.RowsEstimated, x.Bytes)
                ),
            ])
        );

    private IResult List() =>
        Results.Ok(
            new DatabaseBackupsResponse([.. backups.List().Select(View)], backups.KeepMinutes)
        );

    private async Task<IResult> BuildAsync(
        HttpContext http,
        DatabaseBackupRequest request,
        CancellationToken ct
    )
    {
        var identity = AdminIdentity.Of(http);
        var requested = (request.Tables ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        if (requested.Count == 0)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "Choose the tables to back up."
            );

        return await jobs.RunAsync(
                identity.PlayerId,
                TITLE,
                workCt => BuildWorkAsync(identity, requested, workCt),
                ct
            )
            .ConfigureAwait(false);
    }

    private async Task<IResult> BuildWorkAsync(
        AdminIdentity identity,
        HashSet<string> requested,
        CancellationToken ct
    )
    {
        var known = await backups.TablesAsync(ct).ConfigureAwait(false);
        var missing = requested
            .Except(known.Select(x => x.Name), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (missing.Count > 0)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                $"The database has no table {string.Join(", ", missing)}. Load the list again."
            );

        // In the database's order, whatever order they were asked in.
        var tables = known.Select(x => x.Name).Where(requested.Contains).ToList();
        var arguments = tables.Count == known.Length ? "all tables" : string.Join(" ", tables);
        AdminDatabaseBackup? backup;

        try
        {
            backup = await backups
                .BuildAsync(identity.PlayerId, identity.Name, tables, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Kept in the command log as failed; AdminJobs logs the exception and answers 500.
            await audit
                .WriteAsync(
                    AdminActionLog.DATABASE,
                    identity.PlayerId,
                    $"backup {arguments}",
                    CommandOutcome.Failed,
                    CancellationToken.None
                )
                .ConfigureAwait(false);

            throw;
        }

        if (backup is null)
            return AdminResults.Error(
                StatusCodes.Status409Conflict,
                "A backup is being built already. Build yours when it is done."
            );

        await audit
            .WriteAsync(
                AdminActionLog.DATABASE,
                identity.PlayerId,
                $"backup {backup.FileName} {arguments}",
                CommandOutcome.Completed,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        return Results.Ok(View(backup));
    }

    private async Task<IResult> DownloadAsync(Guid id, HttpContext http)
    {
        if (backups.Open(id) is not { } open)
            return AdminResults.Error(StatusCodes.Status404NotFound, GONE);

        await audit
            .WriteAsync(
                AdminActionLog.DATABASE,
                AdminIdentity.Of(http).PlayerId,
                $"download {open.Backup.FileName}",
                CommandOutcome.Completed,
                // Not the request's: the file is open, and is closed only by sending it.
                CancellationToken.None
            )
            .ConfigureAwait(false);

        // Streamed from its file, which the result closes when it is sent.
        return Results.File(open.File, "application/sql", open.Backup.FileName);
    }

    private async Task<IResult> DeleteAsync(Guid id, HttpContext http)
    {
        if (backups.Delete(id) is not { } backup)
            return AdminResults.Error(StatusCodes.Status404NotFound, GONE);

        await audit
            .WriteAsync(
                AdminActionLog.DATABASE,
                AdminIdentity.Of(http).PlayerId,
                $"delete {backup.FileName}",
                CommandOutcome.Completed,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        return Results.NoContent();
    }

    private static DatabaseBackupItem View(AdminDatabaseBackup backup) =>
        new(
            backup.Id,
            backup.FileName,
            [.. backup.Tables],
            backup.Rows,
            backup.Bytes,
            backup.BuiltBy.Value,
            backup.BuiltByName,
            backup.BuiltAtUtc,
            backup.KeptUntilUtc
        );
}
