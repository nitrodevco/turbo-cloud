using Turbo.Database.Migrations;

namespace Turbo.Database.Configuration;

public class DatabaseConfig
{
    public const string SECTION_NAME = "Turbo:Database";

    public string ConnectionString { get; init; } = string.Empty;
    public bool LoggingEnabled { get; init; } = false;

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

    /// <summary>
    /// The database server, such as <c>mysql 8.4.0</c> or <c>mariadb 11.4.0</c>, instead of asking
    /// it at startup. Lets <c>Turbo.Main migrate --script</c> write the SQL without a connection.
    /// Empty asks the server.
    /// </summary>
    public string ServerVersion { get; init; } = string.Empty;
}
