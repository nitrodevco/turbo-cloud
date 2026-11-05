using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A table of the database to back up: its rows (MySQL's estimate when
/// <see cref="RowsEstimated"/>) and the room it takes, when the database says.
/// </summary>
public sealed record DatabaseTableItem(string Name, long Rows, bool RowsEstimated, long? Bytes);

/// <summary>The database's tables, by name.</summary>
public sealed record DatabaseTablesResponse(DatabaseTableItem[] Tables);

/// <summary>
/// A backup built in the panel, kept to download until <see cref="KeptUntilUtc"/>:
/// <c>GET /api/database/backups/{id}/file</c>.
/// </summary>
public sealed record DatabaseBackupItem(
    Guid Id,
    string FileName,
    string[] Tables,
    long Rows,
    long Bytes,
    int BuiltById,
    string BuiltByName,
    DateTime BuiltAtUtc,
    DateTime KeptUntilUtc
);

/// <summary>The backups kept to download, the newest first, and how long each is kept.</summary>
public sealed record DatabaseBackupsResponse(DatabaseBackupItem[] Items, int KeepMinutes);

/// <summary>A backup to build: the names of the tables in it.</summary>
public sealed record DatabaseBackupRequest(string[]? Tables);
