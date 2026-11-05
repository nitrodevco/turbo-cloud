using System.Data.Common;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Turbo.Database.Backups;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// Writing tables out as SQL (<see cref="DatabaseDump"/>), the panel's
/// database backup and a plugin's table export: what it writes, run against the database again,
/// puts the tables back exactly, however many statements their rows take. On a real MySQL or
/// MariaDB (<c>TURBO_TEST_MYSQL</c>, see <see cref="MySqlMigrationTests"/>) that holds for text with
/// quotes, backslashes, line breaks and NULs, times, binary, a row whose AUTO_INCREMENT id is 0 and
/// a TIME past a day.
/// </summary>
public sealed class DatabaseDumpTests : IAsyncLifetime
{
    private static readonly string? SERVER = Environment.GetEnvironmentVariable("TURBO_TEST_MYSQL");

    private readonly SqliteDb _db = new();
    private readonly string _database = "turbo_test_" + Guid.NewGuid().ToString("N")[..12];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        _db.Dispose();

        if (SERVER is null)
            return;

        await using var connection = new MySqlConnection(SERVER);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{_database}`";
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Rows_past_the_statement_size_go_into_more_inserts_and_all_come_back()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        await ExecuteAsync(
            db.Database.GetDbConnection(),
            "CREATE TABLE dump_rows (id INTEGER PRIMARY KEY, text TEXT)"
        );

        for (var i = 1; i <= 200; i++)
            await ExecuteAsync(
                db.Database.GetDbConnection(),
                $"INSERT INTO dump_rows VALUES ({i}, 'row {i} {new string('x', 30)}')"
            );

        var before = await ReadAsync(db.Database.GetDbConnection(), "dump_rows");
        var sql = await DumpAsync(db, new DatabaseConfig { DumpStatementKb = 1 }, "dump_rows");
        var inserts = sql.Split(";\n").Where(x => x.Contains("INSERT INTO")).ToList();

        inserts.Should().HaveCountGreaterThan(5);
        inserts.Should().OnlyContain(x => Encoding.UTF8.GetByteCount(x) <= 1024);

        await ExecuteAsync(db.Database.GetDbConnection(), "DELETE FROM dump_rows");
        await ExecuteAsync(db.Database.GetDbConnection(), sql);

        (await ReadAsync(db.Database.GetDbConnection(), "dump_rows"))
            .Should()
            .BeEquivalentTo(before, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task A_line_break_in_the_title_cannot_end_its_comment()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        var output = new StringWriter();

        await TestDatabaseDump
            .Create()
            .WriteAsync(db, [], "Staff\nDROP TABLE players; --", output, Ct);

        output
            .ToString()
            .Split('\n')
            .Should()
            .NotContain(x => x.StartsWith("DROP TABLE players", StringComparison.Ordinal));
    }

    [Fact]
    public async Task On_MySQL_a_table_comes_back_exactly()
    {
        Assert.SkipUnless(SERVER is not null, "TURBO_TEST_MYSQL is not set");

        await using (var server = new MySqlConnection(SERVER))
        {
            await server.OpenAsync(Ct);
            await ExecuteAsync(
                server,
                $"CREATE DATABASE `{_database}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci"
            );
        }

        await using var db = new TurboDbContext(
            new DbContextOptionsBuilder<TurboDbContext>()
                .UseMySql(
                    new MySqlConnectionStringBuilder(SERVER!)
                    {
                        Database = _database,
                    }.ConnectionString,
                    ServerVersion.AutoDetect(SERVER!)
                )
                .Options
        );
        await db.Database.OpenConnectionAsync(Ct);

        var connection = db.Database.GetDbConnection();

        await ExecuteAsync(
            connection,
            """
            CREATE TABLE `dump_values` (
              `id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
              `text` TEXT NULL,
              `name` VARCHAR(64) NULL,
              `at` DATETIME(6) NULL,
              `stamp` TIMESTAMP(6) NULL,
              `data` BLOB NULL,
              `price` DECIMAL(12,4) NULL,
              `ratio` DOUBLE NULL,
              `flag` TINYINT(1) NULL,
              `span` TIME(6) NULL
            ) DEFAULT CHARSET=utf8mb4
            """
        );
        await ExecuteAsync(
            connection,
            "SET SESSION sql_mode = CONCAT(@@sql_mode, ',NO_AUTO_VALUE_ON_ZERO')"
        );
        await ExecuteAsync(
            connection,
            """
            INSERT INTO `dump_values` VALUES
            (0, 'quote '' backslash \\ newline \n cr \r nul \0 ctrl-z \Z tab \t emoji 🎉', 'O''Brien',
             '2026-11-05 14:30:00.123456', '2026-11-05 14:30:00.654321', X'000102FF', 12.3456, 0.1, 1, '26:03:04.500000'),
            (7, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
            (8, '', '\\''', '1999-01-01 00:00:00', NULL, X'', -0.5, 1e300, 0, '-01:02:03')
            """
        );

        var before = await ReadAsync(connection, "`dump_values`");

        // The list the panel chooses from, from INFORMATION_SCHEMA: MySQL's estimates.
        (await DatabaseDump.TablesAsync(db, Ct))
            .Should()
            .ContainSingle(x => x.Name == "dump_values" && x.RowsEstimated && x.Bytes > 0);

        var sql = await DumpAsync(db, new DatabaseConfig(), "dump_values");

        await ExecuteAsync(connection, "DROP TABLE `dump_values`");
        await db.Database.CloseConnectionAsync();

        // A session of its own, as a restore is.
        await using (
            var restore = new MySqlConnection(
                new MySqlConnectionStringBuilder(SERVER!) { Database = _database }.ConnectionString
            )
        )
        {
            await restore.OpenAsync(Ct);
            await ExecuteAsync(restore, sql);
        }

        await db.Database.OpenConnectionAsync(Ct);

        (await ReadAsync(db.Database.GetDbConnection(), "`dump_values`"))
            .Should()
            .BeEquivalentTo(before, o => o.WithStrictOrdering());
    }

    private static async Task<string> DumpAsync(
        TurboDbContext db,
        DatabaseConfig config,
        params string[] tables
    )
    {
        var output = new StringWriter();

        await TestDatabaseDump.Create(config).WriteAsync(db, tables, "A test", output, Ct);

        return output.ToString();
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Ct);
    }

    /// <summary>Every row of the table by its first column, each value as the provider reads it.</summary>
    private static async Task<List<object[]>> ReadAsync(DbConnection connection, string table)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = $"SELECT * FROM {table} ORDER BY 1";

        await using var reader = await command.ExecuteReaderAsync(Ct);
        var rows = new List<object[]>();

        while (await reader.ReadAsync(Ct))
        {
            var row = new object[reader.FieldCount];

            reader.GetValues(row);
            rows.Add(row);
        }

        return rows;
    }
}
