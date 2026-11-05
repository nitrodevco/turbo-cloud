using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Admin.Configuration;
using Turbo.Database.Backups;
using Turbo.Database.Context;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Backups;

/// <summary>
/// Backups of the hotel's database built in the panel (<c>admin.database.backup</c>): the tables
/// chosen, written as SQL (<see cref="DatabaseDump"/>) to a file of its own under
/// <see cref="AdminConfig.DatabaseBackupDirectory"/>, readable by the server's user only, kept
/// <see cref="AdminConfig.DatabaseBackupKeepMinutes"/> to download and then deleted. A file is
/// written whole before it is listed, so a failure leaves nothing, never a cut-off backup. One is
/// built at a time. The list is this process's: stopping deletes its files, and a file left by a
/// server that did not stop cleanly is never offered and is deleted once its time is up.
/// </summary>
internal sealed class AdminDatabaseBackups(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    DatabaseDump dump,
    IOptions<AdminConfig> config,
    TimeProvider time,
    ILogger<AdminDatabaseBackups> logger
) : IHostedService, IDisposable
{
    /// <summary>What every backup's file is named, so clearing the folder takes only those.</summary>
    private const string FILE_PREFIX = "turbo-backup-";
    private const string FILE_EXTENSION = ".sql";
    private const string PARTIAL_EXTENSION = ".partial";
    private const string DEFAULT_FOLDER = "turbo-backups";

    private readonly ConcurrentDictionary<Guid, AdminDatabaseBackup> _backups = new();
    private readonly SemaphoreSlim _building = new(1, 1);

    /// <summary>How long a backup is kept to download: at least a minute.</summary>
    public int KeepMinutes => Math.Max(1, config.Value.DatabaseBackupKeepMinutes);

    /// <summary>The database's tables, to choose from.</summary>
    public async Task<ImmutableArray<DatabaseTableInfo>> TablesAsync(CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        return await DatabaseDump.TablesAsync(db, ct).ConfigureAwait(false);
    }

    /// <summary>The backups kept to download, the newest first.</summary>
    public ImmutableArray<AdminDatabaseBackup> List()
    {
        Prune();

        return [.. _backups.Values.OrderByDescending(x => x.BuiltAtUtc)];
    }

    /// <summary>
    /// Builds a backup of <paramref name="tables"/>, tables the database has, in that order.
    /// Null when another is being built.
    /// </summary>
    public async Task<AdminDatabaseBackup?> BuildAsync(
        PlayerId by,
        string byName,
        IReadOnlyList<string> tables,
        CancellationToken ct
    )
    {
        if (!await _building.WaitAsync(0, ct).ConfigureAwait(false))
            return null;

        try
        {
            Prune();

            var id = Guid.NewGuid();
            var path = Path.Combine(CreateFolder(), $"{FILE_PREFIX}{id:N}{FILE_EXTENSION}");
            var partial = path + PARTIAL_EXTENSION;
            var builtAt = time.GetUtcNow().UtcDateTime;
            string database;
            long rows;

            try
            {
                var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
                await using var dbScope = db.ConfigureAwait(false);

                database = db.Database.GetDbConnection().Database;

                // The writer closes the file before it is moved into place.
                var writer = new StreamWriter(
                    new FileStream(partial, PrivateFile()),
                    new UTF8Encoding(false)
                );
                await using (writer.ConfigureAwait(false))
                    rows = await dump.WriteAsync(
                            db,
                            tables,
                            $"A backup of {tables.Count.ToString(CultureInfo.InvariantCulture)} tables of the database {database}, built in the admin panel by {byName}.",
                            writer,
                            ct
                        )
                        .ConfigureAwait(false);

                File.Move(partial, path);
            }
            finally
            {
                // Built whole or not at all: there is never a cut-off backup to download.
                if (File.Exists(partial))
                    TryDelete(partial);
            }

            var backup = new AdminDatabaseBackup(
                id,
                path,
                $"{FileNameOf(database)}-{builtAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}{FILE_EXTENSION}",
                [.. tables],
                rows,
                new FileInfo(path).Length,
                by,
                byName,
                builtAt,
                builtAt.AddMinutes(KeepMinutes)
            );

            _backups[id] = backup;

            logger.LogInformation(
                "Player {PlayerId} built a backup of {Tables} tables of the database ({Rows} rows, {Bytes} bytes) as {File}",
                by.Value,
                tables.Count,
                rows,
                backup.Bytes,
                path
            );

            return backup;
        }
        finally
        {
            _building.Release();
        }
    }

    /// <summary>
    /// The backup and its file to read, or null when it is gone. Deleting it while it is read
    /// takes it away once the reading is done.
    /// </summary>
    public (AdminDatabaseBackup Backup, Stream File)? Open(Guid id)
    {
        Prune();

        if (!_backups.TryGetValue(id, out var backup))
            return null;

        if (!File.Exists(backup.Path))
        {
            _backups.TryRemove(id, out _);

            return null;
        }

        return (
            backup,
            new FileStream(
                backup.Path,
                new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.Read | FileShare.Delete,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                }
            )
        );
    }

    /// <summary>Deletes the backup and its file; null when there was none.</summary>
    public AdminDatabaseBackup? Delete(Guid id)
    {
        if (!_backups.TryRemove(id, out var backup))
            return null;

        TryDelete(backup.Path);

        return backup;
    }

    public Task StartAsync(CancellationToken ct)
    {
        Prune();

        return Task.CompletedTask;
    }

    /// <summary>A stopped server offers no backup again, so it keeps none of their files.</summary>
    public Task StopAsync(CancellationToken ct)
    {
        foreach (var id in _backups.Keys)
            Delete(id);

        return Task.CompletedTask;
    }

    public void Dispose() => _building.Dispose();

    /// <summary>
    /// Forgets and deletes the backups past their time, and the files of backups past their time
    /// that this server does not list: left by one that did not stop cleanly.
    /// </summary>
    private void Prune()
    {
        var now = time.GetUtcNow().UtcDateTime;

        foreach (var backup in _backups.Values.Where(x => x.KeptUntilUtc <= now))
            if (_backups.TryRemove(backup.Id, out _))
                TryDelete(backup.Path);

        var folder = Folder();

        if (!Directory.Exists(folder))
            return;

        var listed = _backups
            .Values.Select(x => x.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var before = now.AddMinutes(-KeepMinutes);

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, FILE_PREFIX + "*"))
                if (
                    IsBackupFile(file)
                    && !listed.Contains(file)
                    && File.GetLastWriteTimeUtc(file) < before
                )
                    TryDelete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not look for old database backups in {Folder}", folder);
        }
    }

    private string Folder() =>
        string.IsNullOrWhiteSpace(config.Value.DatabaseBackupDirectory)
            ? Path.Combine(Path.GetTempPath(), DEFAULT_FOLDER)
            : Path.GetFullPath(config.Value.DatabaseBackupDirectory, AppContext.BaseDirectory);

    private string CreateFolder()
    {
        var folder = Folder();

        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(folder);
        else
            Directory.CreateDirectory(
                folder,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );

        return folder;
    }

    /// <summary>A new file only the server's user may read: it holds the whole of the tables.</summary>
    private static FileStreamOptions PrivateFile()
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        };

        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        return options;
    }

    /// <summary>A backup's file or one being written, by its name: the prefix, an id, and the extension.</summary>
    private static bool IsBackupFile(string file)
    {
        var name = Path.GetFileName(file);

        if (name.EndsWith(PARTIAL_EXTENSION, StringComparison.Ordinal))
            name = name[..^PARTIAL_EXTENSION.Length];

        return name.StartsWith(FILE_PREFIX, StringComparison.Ordinal)
            && name.EndsWith(FILE_EXTENSION, StringComparison.Ordinal)
            && Guid.TryParseExact(name[FILE_PREFIX.Length..^FILE_EXTENSION.Length], "N", out _);
    }

    /// <summary>The database's name as it can stand in a file name.</summary>
    private static string FileNameOf(string database)
    {
        var name = new string([
            .. database.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_'),
        ]);

        return name.Length == 0 ? "database" : name;
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete the database backup {File}", path);
        }
    }
}
