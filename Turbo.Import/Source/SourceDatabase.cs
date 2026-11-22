using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace Turbo.Import.Source;

/// <summary>
/// The database an import reads, read only. It learns the tables and columns there are once, as
/// it opens, so a step can ask whether a hotel has a table or a column before it reads it: hotels
/// run different versions of the same emulator, with tables added, renamed and left out.
/// </summary>
internal sealed class SourceDatabase : IAsyncDisposable
{
    private readonly DbConnection _conn;
    private readonly bool _ownsConnection;
    private readonly Dictionary<string, HashSet<string>> _columns = new(
        StringComparer.OrdinalIgnoreCase
    );

    private SourceDatabase(DbConnection conn, bool ownsConnection)
    {
        _conn = conn;
        _ownsConnection = ownsConnection;
    }

    /// <summary>Opens a MySQL or MariaDB database by connection string.</summary>
    public static async Task<SourceDatabase> OpenMySqlAsync(
        string connectionString,
        CancellationToken ct
    )
    {
        var builder = new MySqlConnectionStringBuilder(connectionString)
        {
            // Old rows hold '0000-00-00' where nothing was set; read them as NULL.
            ConvertZeroDateTime = true,
            // An import reads the biggest tables whole.
            DefaultCommandTimeout = 0,
        };

        var conn = new MySqlConnection(builder.ConnectionString);

        try
        {
            await conn.OpenAsync(ct).ConfigureAwait(false);

            return await OpenAsync(conn, ownsConnection: true, ct).ConfigureAwait(false);
        }
        catch
        {
            await conn.DisposeAsync().ConfigureAwait(false);

            throw;
        }
    }

    /// <summary>
    /// Wraps a connection that is already open. The caller keeps it: disposing this leaves it open
    /// (a test's in-memory database lives as long as its connection).
    /// </summary>
    public static Task<SourceDatabase> OpenAsync(DbConnection conn, CancellationToken ct) =>
        OpenAsync(conn, ownsConnection: false, ct);

    private static async Task<SourceDatabase> OpenAsync(
        DbConnection conn,
        bool ownsConnection,
        CancellationToken ct
    )
    {
        var db = new SourceDatabase(conn, ownsConnection);

        await db.LoadSchemaAsync(ct).ConfigureAwait(false);

        return db;
    }

    /// <summary>The name of the database read, for the report.</summary>
    public string Name => _conn.Database;

    public bool HasTable(string table) => _columns.ContainsKey(table);

    public bool HasColumn(string table, string column) =>
        _columns.TryGetValue(table, out var columns) && columns.Contains(column);

    /// <summary>
    /// Every row of a table, in the order of <paramref name="orderBy"/> when the table has that
    /// column. A table the database does not have yields nothing.
    /// </summary>
    public async IAsyncEnumerable<SourceRow> ReadAsync(
        string table,
        string? orderBy = "id",
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await foreach (var row in ReadWhereAsync(table, null, orderBy, ct).ConfigureAwait(false))
            yield return row;
    }

    /// <summary>
    /// The rows of a table that <paramref name="where"/> keeps: a condition the importer writes
    /// itself, over columns it has checked are there, never text from either database.
    /// </summary>
    public async IAsyncEnumerable<SourceRow> ReadWhereAsync(
        string table,
        string? where,
        string? orderBy = "id",
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        if (!HasTable(table))
            yield break;

        var sql = $"SELECT * FROM {Quote(table)}";

        if (where is not null)
            sql += $" WHERE {where}";

        if (orderBy is not null && HasColumn(table, orderBy))
            sql += $" ORDER BY {Quote(orderBy)}";

        SourceRow? row = null;

        await foreach (var reader in QueryAsync(sql, ct).ConfigureAwait(false))
            yield return row ??= new SourceRow(reader);
    }

    /// <summary>The distinct values of a column; none for a table or column the database lacks.</summary>
    public async Task<List<string>> DistinctAsync(string table, string column, CancellationToken ct)
    {
        var values = new List<string>();

        if (!HasColumn(table, column))
            return values;

        var sql =
            $"SELECT DISTINCT {Quote(column)} FROM {Quote(table)} WHERE {Quote(column)} IS NOT NULL";

        await foreach (var reader in QueryAsync(sql, ct).ConfigureAwait(false))
            values.Add(
                Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty
            );

        return values;
    }

    /// <summary>The number of rows in a table; 0 for one the database does not have.</summary>
    public async Task<long> CountAsync(string table, CancellationToken ct)
    {
        if (!HasTable(table))
            return 0;

        long count = 0;

        await foreach (
            var reader in QueryAsync($"SELECT COUNT(*) FROM {Quote(table)}", ct)
                .ConfigureAwait(false)
        )
            count = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);

        return count;
    }

    /// <summary>The rows of one query, a reader positioned on each in turn.</summary>
    private async IAsyncEnumerable<DbDataReader> QueryAsync(
        string sql,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        var cmd = _conn.CreateCommand();

        await using var cmdScope = cmd.ConfigureAwait(false);

        cmd.CommandText = sql;
        cmd.CommandTimeout = 0;

        var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

        await using var readerScope = reader.ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            yield return reader;
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsConnection)
            await _conn.DisposeAsync().ConfigureAwait(false);
    }

    private bool IsSqlite => _conn.GetType().Name.StartsWith("Sqlite", StringComparison.Ordinal);

    private string Quote(string name) =>
        IsSqlite
            ? $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : $"`{name.Replace("`", "``", StringComparison.Ordinal)}`";

    private async Task LoadSchemaAsync(CancellationToken ct)
    {
        if (IsSqlite)
        {
            var tables = new List<string>();

            await foreach (
                var reader in QueryAsync(
                        "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'",
                        ct
                    )
                    .ConfigureAwait(false)
            )
                tables.Add(reader.GetString(0));

            foreach (var table in tables)
            await foreach (
                var reader in QueryAsync(
                        $"SELECT name FROM pragma_table_info({QuoteLiteral(table)})",
                        ct
                    )
                    .ConfigureAwait(false)
            )
                Add(table, reader.GetString(0));

            return;
        }

        await foreach (
            var reader in QueryAsync(
                    "SELECT table_name, column_name FROM information_schema.columns "
                        + "WHERE table_schema = DATABASE()",
                    ct
                )
                .ConfigureAwait(false)
        )
            Add(reader.GetString(0), reader.GetString(1));

        if (_columns.Count == 0)
            throw new ImportException(
                $"The source database '{_conn.Database}' has no tables. Check the connection string's Database."
            );
    }

    private void Add(string table, string column)
    {
        if (!_columns.TryGetValue(table, out var columns))
            _columns[table] = columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        columns.Add(column);
    }

    private static string QuoteLiteral(string value) =>
        $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
