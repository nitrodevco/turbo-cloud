using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;
using Turbo.Database.Context;

namespace Turbo.Database.Backups;

/// <summary>
/// The database's tables, and writing them out as SQL that puts them back: each table dropped if
/// it is there, created as it is now, and its rows inserted, several to a statement
/// (<see cref="DatabaseConfig.DumpStatementKb"/>). Every table is read in one transaction (on
/// MySQL a consistent snapshot), so tables that refer to each other are written as they were at
/// one moment, not each as it was when its turn came. The admin panel's database backup and a
/// plugin's table export both write through it.
/// </summary>
public sealed class DatabaseDump(IOptions<DatabaseConfig> config, TimeProvider time)
{
    private const string DATE_FORMAT = "yyyy-MM-dd HH:mm:ss.ffffff";

    /// <summary>The names of the database's tables, its views and SQLite's own left out.</summary>
    public static async Task<List<string>> TableNamesAsync(TurboDbContext db, CancellationToken ct)
    {
        var names = await db
            .Database.SqlQueryRaw<string>(
                IsSqlite(db)
                    ? @"SELECT name AS Value FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite\_%' ESCAPE '\'"
                    : "SELECT TABLE_NAME AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'"
            )
            .ToListAsync(ct)
            .ConfigureAwait(false);

        names.Sort(StringComparer.OrdinalIgnoreCase);

        return names;
    }

    /// <summary>
    /// The database's tables by name, each with its rows and size. MySQL's row counts are the
    /// estimates it keeps (counting reads the whole table) and may be a day old on MySQL 8;
    /// SQLite's are counted, and it says no size.
    /// </summary>
    public static async Task<ImmutableArray<DatabaseTableInfo>> TablesAsync(
        TurboDbContext db,
        CancellationToken ct
    )
    {
        var sqlite = IsSqlite(db);
        var tables = ImmutableArray.CreateBuilder<DatabaseTableInfo>();

        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);

        try
        {
            var connection = db.Database.GetDbConnection();

            if (sqlite)
            {
                foreach (var name in await TableNamesAsync(db, ct).ConfigureAwait(false))
                    tables.Add(
                        new DatabaseTableInfo(
                            name,
                            Convert.ToInt64(
                                await ScalarAsync(
                                        connection,
                                        null,
                                        $"SELECT COUNT(*) FROM {Quote(name, sqlite)}",
                                        ct
                                    )
                                    .ConfigureAwait(false),
                                CultureInfo.InvariantCulture
                            ),
                            false,
                            null
                        )
                    );
            }
            else
            {
                var command = connection.CreateCommand();
                await using var commandScope = command.ConfigureAwait(false);

                // row_count, not rows: ROWS is a reserved word on MySQL 8.
                command.CommandText =
                    "SELECT TABLE_NAME, COALESCE(TABLE_ROWS, 0) AS row_count, COALESCE(DATA_LENGTH, 0) + COALESCE(INDEX_LENGTH, 0) AS byte_count FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'";

                var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                await using var readerScope = reader.ConfigureAwait(false);

                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    tables.Add(
                        new DatabaseTableInfo(
                            reader.GetString(0),
                            Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                            true,
                            Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture)
                        )
                    );
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        tables.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));

        return tables.ToImmutable();
    }

    /// <summary>
    /// Writes <paramref name="tables"/> to <paramref name="writer"/> as SQL, under a comment
    /// naming <paramref name="title"/>, and answers how many rows it wrote. Foreign key checks are
    /// off while it runs, so the tables may come in any order.
    /// </summary>
    public async Task<long> WriteAsync(
        TurboDbContext db,
        IReadOnlyList<string> tables,
        string title,
        TextWriter writer,
        CancellationToken ct
    )
    {
        var sqlite = IsSqlite(db);
        var maxStatementBytes = Math.Max(1, config.Value.DumpStatementKb) * 1024;
        var rows = 0L;

        // The same line ends on every server, as the rows inside a statement have.
        writer.NewLine = "\n";

        // One line: a line break in a name would end the comment and run the rest as SQL.
        await writer.WriteLineAsync($"-- {title.ReplaceLineEndings(" ")}").ConfigureAwait(false);
        await writer
            .WriteLineAsync(
                $"-- Written {time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC: {tables.Count.ToString(CultureInfo.InvariantCulture)} tables. Running it drops each table and creates it again as it was."
            )
            .ConfigureAwait(false);

        if (sqlite)
            await writer.WriteLineAsync("PRAGMA foreign_keys = OFF;").ConfigureAwait(false);
        else
        {
            // As read: text in utf8mb4, times in UTC, and a 0 in an AUTO_INCREMENT column kept as 0.
            // SQL_MODE also leaves out NO_BACKSLASH_ESCAPES, which the text's escapes need.
            await writer.WriteLineAsync("SET NAMES utf8mb4;").ConfigureAwait(false);
            await writer.WriteLineAsync("SET TIME_ZONE = '+00:00';").ConfigureAwait(false);
            await writer
                .WriteLineAsync("SET SQL_MODE = 'NO_AUTO_VALUE_ON_ZERO';")
                .ConfigureAwait(false);
            await writer.WriteLineAsync("SET FOREIGN_KEY_CHECKS = 0;").ConfigureAwait(false);
        }

        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);

        try
        {
            var connection = db.Database.GetDbConnection();

            // TIMESTAMP columns are read in the session's zone: UTC, as the file sets it.
            if (!sqlite)
                await ScalarAsync(connection, null, "SET time_zone = '+00:00'", ct)
                    .ConfigureAwait(false);

            // Snapshot is MySqlConnector's START TRANSACTION WITH CONSISTENT SNAPSHOT. It only
            // reads, so it is never committed: disposing it rolls nothing back.
            var transaction = await connection
                .BeginTransactionAsync(
                    sqlite ? IsolationLevel.Serializable : IsolationLevel.Snapshot,
                    ct
                )
                .ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);

            foreach (var table in tables)
                rows += await WriteTableAsync(
                        connection,
                        transaction,
                        table,
                        sqlite,
                        maxStatementBytes,
                        writer,
                        ct
                    )
                    .ConfigureAwait(false);
        }
        finally
        {
            await db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer
            .WriteLineAsync(sqlite ? "PRAGMA foreign_keys = ON;" : "SET FOREIGN_KEY_CHECKS = 1;")
            .ConfigureAwait(false);
        await writer.FlushAsync(ct).ConfigureAwait(false);

        return rows;
    }

    public static bool IsSqlite(TurboDbContext db) =>
        db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>A table or column name quoted for the database: backticks on MySQL, double quotes on SQLite.</summary>
    public static string Quote(TurboDbContext db, string name) => Quote(name, IsSqlite(db));

    private static string Quote(string name, bool sqlite) =>
        sqlite ? $"\"{name.Replace("\"", "\"\"")}\"" : $"`{name.Replace("`", "``")}`";

    private static async Task<long> WriteTableAsync(
        DbConnection connection,
        DbTransaction transaction,
        string table,
        bool sqlite,
        int maxStatementBytes,
        TextWriter writer,
        CancellationToken ct
    )
    {
        var quoted = Quote(table, sqlite);

        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync($"DROP TABLE IF EXISTS {quoted};").ConfigureAwait(false);
        await writer
            .WriteLineAsync(
                await CreateStatementAsync(connection, transaction, table, quoted, sqlite, ct)
                    .ConfigureAwait(false) + ";"
            )
            .ConfigureAwait(false);

        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);

        command.Transaction = transaction;
        command.CommandText = $"SELECT * FROM {quoted}";
        // A table of millions of rows takes as long as it takes to read; the token stops it.
        command.CommandTimeout = 0;

        var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await using var readerScope = reader.ConfigureAwait(false);

        var insert =
            $"INSERT INTO {quoted} ({string.Join(", ", Enumerable.Range(0, reader.FieldCount).Select(i => Quote(reader.GetName(i), sqlite)))}) VALUES";
        var insertBytes = Encoding.UTF8.GetByteCount(insert);
        var statement = new StringBuilder();
        var statementBytes = 0;
        var values = new string[reader.FieldCount];
        var rows = 0L;

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            for (var i = 0; i < values.Length; i++)
                values[i] = Literal(reader.GetValue(i), sqlite);

            var row = $"({string.Join(", ", values)})";
            // The row, and the ",\n" before it.
            var rowBytes = Encoding.UTF8.GetByteCount(row) + 2;

            if (statement.Length > 0 && statementBytes + rowBytes > maxStatementBytes)
            {
                await writer.WriteLineAsync(statement.Append(';')).ConfigureAwait(false);
                statement.Clear();
            }

            if (statement.Length == 0)
            {
                statement.Append(insert);
                statementBytes = insertBytes;
            }
            else
                statement.Append(',');

            statement.Append('\n').Append(row);
            statementBytes += rowBytes;
            rows++;
        }

        if (statement.Length > 0)
            await writer.WriteLineAsync(statement.Append(';')).ConfigureAwait(false);

        return rows;
    }

    private static async Task<string> CreateStatementAsync(
        DbConnection connection,
        DbTransaction transaction,
        string table,
        string quoted,
        bool sqlite,
        CancellationToken ct
    )
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);

        command.Transaction = transaction;
        command.CommandText = sqlite
            ? $"SELECT sql FROM sqlite_master WHERE type = 'table' AND name = '{table.Replace("'", "''")}'"
            : $"SHOW CREATE TABLE {quoted}";

        var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await using var readerScope = reader.ConfigureAwait(false);

        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException($"The table {table} has no definition to write.");

        return reader.GetString(sqlite ? 0 : 1);
    }

    private static async Task<object?> ScalarAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken ct
    )
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);

        command.Transaction = transaction;
        command.CommandText = sql;

        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }

    private static string Literal(object value, bool sqlite) =>
        value switch
        {
            DBNull => "NULL",
            bool b => b ? "1" : "0",
            byte[] bytes => $"X'{Convert.ToHexString(bytes)}'",
            DateTime d => $"'{d.ToString(DATE_FORMAT, CultureInfo.InvariantCulture)}'",
            DateTimeOffset d =>
                $"'{d.UtcDateTime.ToString(DATE_FORMAT, CultureInfo.InvariantCulture)}'",
            // MySQL's TIME counts hours past a day ('26:00:00'); .NET writes days apart ('1.02:00:00').
            TimeSpan t when !sqlite => Text(
                (t < TimeSpan.Zero ? "-" : "")
                    + ((long)t.Duration().TotalHours).ToString(CultureInfo.InvariantCulture)
                    + t.Duration().ToString(@"\:mm\:ss\.ffffff", CultureInfo.InvariantCulture),
                sqlite
            ),
            string or char or Guid or TimeSpan => Text(
                Convert.ToString(value, CultureInfo.InvariantCulture)!,
                sqlite
            ),
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _ => Text(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", sqlite),
        };

    /// <summary>
    /// A text literal. SQLite has no escapes, only the quote doubled. MySQL's backslash escapes
    /// what its client would trip on, as mysqldump does: a line break or a NUL stays in the value
    /// and out of the file's lines.
    /// </summary>
    private static string Text(string value, bool sqlite)
    {
        if (sqlite)
            return "'" + value.Replace("'", "''") + "'";

        var text = new StringBuilder(value.Length + 2).Append('\'');

        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    text.Append(@"\\");
                    break;
                case '\'':
                    text.Append("''");
                    break;
                case '\0':
                    text.Append(@"\0");
                    break;
                case '\n':
                    text.Append(@"\n");
                    break;
                case '\r':
                    text.Append(@"\r");
                    break;
                case '\x1a':
                    text.Append(@"\Z");
                    break;
                default:
                    text.Append(c);
                    break;
            }
        }

        return text.Append('\'').ToString();
    }
}
