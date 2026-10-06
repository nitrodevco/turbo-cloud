using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;
using Turbo.Database.Migrations;
using Turbo.Tests.Support;

namespace Turbo.Tests.Database;

public sealed class Thing
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>A context whose last migration drops a table: the destructive case.</summary>
public sealed class ThingsContext(DbContextOptions<ThingsContext> options) : DbContext(options)
{
    public DbSet<Thing> Things => Set<Thing>();
}

/// <summary>A context whose second migration is not SQL: the failing case.</summary>
public sealed class BrokenContext(DbContextOptions<BrokenContext> options) : DbContext(options)
{
    public DbSet<Thing> Things => Set<Thing>();
}

/// <summary>Three independent tables, to apply them in an order the ids do not say.</summary>
public sealed class LettersContext(DbContextOptions<LettersContext> options) : DbContext(options)
{
    public DbSet<Thing> Things => Set<Thing>();
}

[DbContext(typeof(ThingsContext))]
[Migration("20260101000001_CreateThings")]
public sealed class CreateThings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            "things",
            table => new
            {
                id = table.Column<int>(nullable: false),
                name = table.Column<string>(nullable: false),
            },
            constraints: table => table.PrimaryKey("pk_things", x => x.id)
        );
}

[DbContext(typeof(ThingsContext))]
[Migration("20260101000002_AddExtras")]
public sealed class AddExtras : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            "extras",
            table => new { id = table.Column<int>(nullable: false) },
            constraints: table => table.PrimaryKey("pk_extras", x => x.id)
        );
}

[DbContext(typeof(ThingsContext))]
[Migration("20260101000003_DropExtras")]
public sealed class DropExtras : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("extras");
}

/// <summary>A migration that drops a column. Only ever refused, never run: SQLite cannot run it.</summary>
public sealed class ColumnsContext(DbContextOptions<ColumnsContext> options) : DbContext(options)
{
    public DbSet<Thing> Things => Set<Thing>();
}

[DbContext(typeof(ColumnsContext))]
[Migration("20260101000001_CreateWithNote")]
public sealed class CreateWithNote : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            "things",
            table => new
            {
                id = table.Column<int>(nullable: false),
                note = table.Column<string>(nullable: true),
            },
            constraints: table => table.PrimaryKey("pk_things", x => x.id)
        );
}

[DbContext(typeof(ColumnsContext))]
[Migration("20260101000002_DropNoteColumn")]
public sealed class DropNoteColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn("note", "things");
}

[DbContext(typeof(BrokenContext))]
[Migration("20260101000001_CreateFirst")]
public sealed class CreateFirst : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            "first",
            table => new { id = table.Column<int>(nullable: false) },
            constraints: table => table.PrimaryKey("pk_first", x => x.id)
        );
}

[DbContext(typeof(BrokenContext))]
[Migration("20260101000002_ThisIsNotSql")]
public sealed class ThisIsNotSql : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("THIS IS NOT SQL");
}

[DbContext(typeof(BrokenContext))]
[Migration("20260101000003_NeverReached")]
public sealed class NeverReached : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            "third",
            table => new { id = table.Column<int>(nullable: false) },
            constraints: table => table.PrimaryKey("pk_third", x => x.id)
        );
}

[DbContext(typeof(LettersContext))]
[Migration("20260101000001_CreateA")]
public sealed class CreateA : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => Table(migrationBuilder, "a");

    internal static void Table(MigrationBuilder migrationBuilder, string name) =>
        migrationBuilder.CreateTable(
            name,
            table => new { id = table.Column<int>(nullable: false) },
            constraints: table => table.PrimaryKey("pk_" + name, x => x.id)
        );
}

[DbContext(typeof(LettersContext))]
[Migration("20260101000002_CreateB")]
public sealed class CreateB : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        CreateA.Table(migrationBuilder, "b");
}

[DbContext(typeof(LettersContext))]
[Migration("20260101000003_CreateC")]
public sealed class CreateC : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        CreateA.Table(migrationBuilder, "c");
}

/// <summary>A lock that records what was asked of it and lets a test play the other process.</summary>
public sealed class FakeMigrationLock : IMigrationLock
{
    public int Acquired { get; private set; }

    public int Released { get; private set; }

    public TimeSpan? LastTimeout { get; private set; }

    /// <summary>Runs once the lock is "taken": another process finishing first, say.</summary>
    public Func<Task>? WhileWaiting { get; set; }

    /// <summary>Thrown instead of taking the lock: it stayed held, or the server went away.</summary>
    public Exception? Fail { get; set; }

    public async Task<IAsyncDisposable> AcquireAsync(
        string connectionString,
        TimeSpan timeout,
        CancellationToken ct
    )
    {
        LastTimeout = timeout;

        if (Fail is not null)
            throw Fail;

        Acquired++;

        if (WhileWaiting is { } other)
            await other().ConfigureAwait(false);

        return new Held(this);
    }

    private sealed class Held(FakeMigrationLock owner) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            owner.Released++;

            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>One SQLite database, kept open, that the contexts and migrators of a test share.</summary>
public sealed class MigratorHarness : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public MigratorHarness()
    {
        _connection.Open();
    }

    public FakeMigrationLock Lock { get; } = new();

    public CapturingLogger<DatabaseMigrator> Log { get; } = new();

    public DatabaseMigrator Migrator(Action<TestConfig>? configure = null)
    {
        var config = new TestConfig();
        configure?.Invoke(config);

        return new DatabaseMigrator(
            Options.Create(
                new DatabaseConfig
                {
                    Migrate = config.Mode,
                    AllowDestructiveMigrations = config.AllowDestructive,
                    MigrationLockSeconds = config.LockSeconds,
                }
            ),
            Lock,
            Log
        );
    }

    public T Context<T>()
        where T : DbContext
    {
        var options = new DbContextOptionsBuilder<T>()
            .UseSqlite(_connection)
            // The test migrations have no model snapshot, which is what this check compares.
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return (T)Activator.CreateInstance(typeof(T), options)!;
    }

    public Task Run(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;

        return command.ExecuteNonQueryAsync();
    }

    public async Task<List<string>> TablesAsync()
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";

        var tables = new List<string>();

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        return tables;
    }

    public async Task<List<string>> HistoryAsync()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId";

        var ids = new List<string>();

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            ids.Add(reader.GetString(0));

        return ids;
    }

    public void Dispose() => _connection.Dispose();

    public sealed class TestConfig
    {
        public MigrationMode Mode { get; set; } = MigrationMode.Auto;

        public bool AllowDestructive { get; set; }

        public int LockSeconds { get; set; } = 60;
    }
}
