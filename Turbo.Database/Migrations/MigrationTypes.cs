using System;
using System.Collections.Generic;
using System.Linq;

namespace Turbo.Database.Migrations;

/// <summary>What the server does about database migrations when it starts (<c>Turbo:Database:Migrate</c>).</summary>
public enum MigrationMode
{
    /// <summary>Apply what is pending, behind a lock, and say what was done. The default.</summary>
    Auto,

    /// <summary>
    /// Apply nothing; stop with a readable message if anything is pending. For a hotel whose deploy
    /// runs <c>Turbo.Main migrate</c> (or its own script) before starting the server.
    /// </summary>
    Check,

    /// <summary>Do not look at the schema at all. Whoever runs the server owns it.</summary>
    Off,
}

public enum MigrationOutcome
{
    /// <summary>The mode is <see cref="MigrationMode.Off"/>: nothing was looked at.</summary>
    Skipped,

    /// <summary>Nothing was pending.</summary>
    UpToDate,

    /// <summary>Pending migrations were applied.</summary>
    Applied,
}

/// <summary>Where a database stands against the migrations this build ships.</summary>
/// <param name="Applied">What the database's history table says is applied, oldest first.</param>
/// <param name="Pending">What this build ships that the database has not applied, oldest first.</param>
/// <param name="Unknown">What the database has applied that this build does not ship.</param>
/// <param name="DatabaseExists">False when the database itself is not there yet.</param>
public sealed record MigrationState(
    IReadOnlyList<string> Applied,
    IReadOnlyList<string> Pending,
    IReadOnlyList<string> Unknown,
    bool DatabaseExists
)
{
    /// <summary>
    /// A pending migration older than one already applied: a migration merged after a newer one
    /// was released. Fine to apply, but only as one pass; migrating "to" it would roll the newer
    /// ones back.
    /// </summary>
    public bool HasOutOfOrderPending =>
        Applied.Count > 0 && Pending.Any(id => string.CompareOrdinal(id, Applied[^1]) < 0);

    public string? Current => Applied.Count > 0 ? Applied[^1] : null;
}

public sealed record MigrationResult(
    MigrationOutcome Outcome,
    IReadOnlyList<string> Applied,
    string? Current,
    TimeSpan Elapsed
);

/// <summary>Per-call overrides of the configured behaviour.</summary>
public sealed record MigrationRunOptions
{
    /// <summary>Instead of <c>Turbo:Database:Migrate</c>. <c>Turbo.Main migrate</c> passes <see cref="MigrationMode.Auto"/>.</summary>
    public MigrationMode? Mode { get; init; }

    /// <summary>Instead of <c>Turbo:Database:AllowDestructiveMigrations</c>.</summary>
    public bool? AllowDestructive { get; init; }

    /// <summary>
    /// Say "up to date" at debug rather than information: a plugin that hot reloads asks every
    /// time, and has nothing to report.
    /// </summary>
    public bool QuietWhenCurrent { get; init; }
}

/// <summary>
/// A migration that cannot or must not run, said in full: the message is written for whoever runs
/// the server and is the whole report, so it is logged without a stack trace.
/// </summary>
public sealed class MigrationException : Exception
{
    public MigrationException(string message)
        : base(message) { }

    public MigrationException(string message, Exception inner)
        : base(message, inner) { }

    /// <summary>
    /// The migration problem behind <paramref name="exception"/>, if there is one. A plugin's
    /// is thrown while the host starts, and reaches the caller wrapped by the host, or by the
    /// plugin loader, in an <see cref="AggregateException"/> or as an inner exception.
    /// </summary>
    public static MigrationException? FindIn(Exception? exception)
    {
        switch (exception)
        {
            case null:
                return null;
            case MigrationException migration:
                return migration;
            case AggregateException aggregate:
                foreach (var inner in aggregate.InnerExceptions)
                {
                    if (FindIn(inner) is { } found)
                        return found;
                }

                return null;
            default:
                return FindIn(exception.InnerException);
        }
    }
}
