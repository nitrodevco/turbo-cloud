using System;
using System.Collections.Immutable;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Backups;

/// <summary>
/// A database backup built in the panel (<see cref="AdminDatabaseBackups"/>): where its file is,
/// the name it is downloaded as, the tables in it and their rows, who built it and until when it
/// is kept.
/// </summary>
internal sealed record AdminDatabaseBackup(
    Guid Id,
    string Path,
    string FileName,
    ImmutableArray<string> Tables,
    long Rows,
    long Bytes,
    PlayerId BuiltBy,
    string BuiltByName,
    DateTime BuiltAtUtc,
    DateTime KeptUntilUtc
);
