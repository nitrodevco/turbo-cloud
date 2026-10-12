using Turbo.Database.Migrations;
using Turbo.Primitives.Settings;

namespace Turbo.Database.Configuration;

/// <summary>
/// The database and how the server migrates it. The admin panel's overrides are read from this
/// database, so none of it can be one of them.
/// </summary>
[StartupSetting]
public class DatabaseConfig
{
    public const string SECTION_NAME = "Turbo:Database";

    /// <summary>The PostgreSQL connection string, its password included.</summary>
    [SecretSetting]
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// The connection string migrations run with, when the server's own role may not change the
    /// schema (<c>docs/database.md</c>, "A role for the server, and one for migrations"). Empty:
    /// migrations use <see cref="ConnectionString"/>. Used for the emulator's tables, every
    /// plugin's, and a plugin's uninstall; nothing else.
    /// </summary>
    [SecretSetting]
    public string MigrationConnectionString { get; init; } = string.Empty;

    public bool LoggingEnabled { get; init; } = false;

    /// <summary>
    /// How many of its most used statements each connection keeps prepared, so PostgreSQL plans
    /// them once rather than every time (Npgsql's automatic preparation): measured on the
    /// inventory and room loads and the wallet update, 20 to 32% faster. 0 turns it off, which a
    /// pooler in transaction mode older than PgBouncer 1.21 needs. A <c>Max Auto Prepare</c> in
    /// the connection string wins.
    /// </summary>
    public int MaxAutoPrepare { get; init; } = 50;

    /// <summary>
    /// What the server does about migrations at startup, for its own tables and for each plugin's:
    /// <see cref="MigrationMode.Auto"/> applies what is pending, <see cref="MigrationMode.Check"/>
    /// refuses to start while anything is, <see cref="MigrationMode.Off"/> does not look.
    /// See <c>docs/database.md</c>.
    /// </summary>
    public MigrationMode Migrate { get; init; } = MigrationMode.Auto;

    /// <summary>
    /// Whether a migration that drops a table or a column may run against a database that has
    /// data. Off, the server refuses and names it; back up, then turn it on for one start.
    /// </summary>
    public bool AllowDestructiveMigrations { get; init; } = false;

    /// <summary>How long a migrator waits for another one (a second server, a deploy script) to finish.</summary>
    public int MigrationLockSeconds { get; init; } = 60;

    /// <summary>
    /// How long one statement of a migration may run. The usual 30 seconds is too short for an
    /// ALTER on a table with millions of rows.
    /// </summary>
    public int MigrationCommandTimeoutMinutes { get; init; } = 30;
}
