using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Migrations;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;

namespace Turbo.Plugins.Data;

/// <summary>
/// A plugin's tables (<see cref="IPluginDataOwner"/>): every table whose name starts with its
/// manifest's <c>TablePrefix</c>, and its migrations' history table. Never a table the hotel's
/// own model maps, nor one of another plugin whose prefix starts with this one's; a prefix that is
/// empty or more than letters, digits and '_' matches nothing. Also writes them out as SQL to keep.
/// </summary>
internal sealed class PluginTables(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<PluginTables> logger
) : IPluginDataOwner
{
    private const string CORE_HISTORY = "__EFMigrationsHistory";

    public async Task<ImmutableArray<PluginDataItem>> FindAsync(
        PluginDataScope plugin,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var items = ImmutableArray.CreateBuilder<PluginDataItem>();

        foreach (var table in await TablesOfAsync(db, plugin, ct).ConfigureAwait(false))
            items.Add(
                new PluginDataItem(
                    PluginDataKind.Table,
                    table,
                    await db
#pragma warning disable EF1002 // A table name cannot be a parameter; it is one the database listed, quoted.
                        .Database.SqlQueryRaw<long>(
                            $"SELECT COUNT(*) AS Value FROM {Quote(db, table)}"
                        )
#pragma warning restore EF1002
                        .SingleAsync(ct)
                        .ConfigureAwait(false)
                )
            );

        return items.ToImmutable();
    }

    public async Task RemoveAsync(
        PluginDataScope plugin,
        ImmutableArray<PluginDataItem> found,
        PlayerId actor,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        // Matched by the prefix again as well: only what is the plugin's now is dropped.
        var listed = found
            .Where(x => x.Kind == PluginDataKind.Table)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tables = (await TablesOfAsync(db, plugin, ct).ConfigureAwait(false))
            .Where(listed.Contains)
            .ToList();

        if (tables.Count == 0)
            return;

        var sqlite = IsSqlite(db);

        // One connection: the foreign key switch is the session's, and its tables may refer to
        // each other in any order.
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);

        try
        {
            await db
                .Database.ExecuteSqlRawAsync(
                    sqlite ? "PRAGMA foreign_keys = OFF" : "SET FOREIGN_KEY_CHECKS = 0",
                    ct
                )
                .ConfigureAwait(false);

            foreach (var table in tables)
#pragma warning disable EF1002 // A table name cannot be a parameter; it is one the database listed, quoted.
                await db
                    .Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS {Quote(db, table)}", ct)
                    .ConfigureAwait(false);
#pragma warning restore EF1002
        }
        finally
        {
            await db
                .Database.ExecuteSqlRawAsync(
                    sqlite ? "PRAGMA foreign_keys = ON" : "SET FOREIGN_KEY_CHECKS = 1",
                    CancellationToken.None
                )
                .ConfigureAwait(false);
            await db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        logger.LogWarning(
            "Player {PlayerId} uninstalled the plugin {Key} with its data: dropped {Tables}",
            actor.Value,
            plugin.Key,
            string.Join(", ", tables)
        );
    }

    /// <summary>
    /// Writes the plugin's tables to <paramref name="output"/> as SQL: each one dropped if it is
    /// there, created as it is now, and its rows inserted, so running it puts them back as they were.
    /// </summary>
    public async Task ExportAsync(PluginDataScope plugin, Stream output, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var tables = await TablesOfAsync(db, plugin, ct).ConfigureAwait(false);
        var sqlite = IsSqlite(db);
        var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        await using var writerScope = writer.ConfigureAwait(false);

        await writer
            .WriteLineAsync(
                $"-- The tables of the plugin {plugin.Key}, exported {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC."
            )
            .ConfigureAwait(false);

        if (!sqlite)
            await writer.WriteLineAsync("SET FOREIGN_KEY_CHECKS = 0;").ConfigureAwait(false);

        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);

        try
        {
            var connection = db.Database.GetDbConnection();

            foreach (var table in tables)
            {
                var quoted = Quote(db, table);

                await writer.WriteLineAsync().ConfigureAwait(false);
                await writer
                    .WriteLineAsync($"DROP TABLE IF EXISTS {quoted};")
                    .ConfigureAwait(false);
                await writer
                    .WriteLineAsync(
                        await CreateStatementAsync(connection, table, quoted, sqlite, ct)
                            .ConfigureAwait(false) + ";"
                    )
                    .ConfigureAwait(false);

                await WriteRowsAsync(connection, quoted, sqlite, writer, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        if (!sqlite)
            await writer.WriteLineAsync("SET FOREIGN_KEY_CHECKS = 1;").ConfigureAwait(false);
    }

    /// <summary>The plugin's tables in the database now, by its prefix, without the hotel's and other plugins'.</summary>
    private static async Task<List<string>> TablesOfAsync(
        TurboDbContext db,
        PluginDataScope plugin,
        CancellationToken ct
    )
    {
        var prefix = plugin.TablePrefix;

        if (!MigrationHelper.IsPlainTablePrefix(prefix))
            return [];

        var core = db
            .Model.GetEntityTypes()
            .Select(x => x.GetTableName())
            .OfType<string>()
            .Append(CORE_HISTORY)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var others = plugin.OtherTablePrefixes.Where(MigrationHelper.IsPlainTablePrefix).ToList();
        var longer = others
            .Where(x =>
                x.Length > prefix!.Length
                && x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            )
            .ToList();
        var history = MigrationHelper.HistoryTableOf(prefix!);
        var historyShared = others.Any(x =>
            string.Equals(
                MigrationHelper.HistoryTableOf(x),
                history,
                StringComparison.OrdinalIgnoreCase
            )
        );

        var names = await db
            .Database.SqlQueryRaw<string>(
                IsSqlite(db)
                    ? "SELECT name AS Value FROM sqlite_master WHERE type = 'table'"
                    : "SELECT TABLE_NAME AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'"
            )
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return
        [
            .. names
                .Where(name => !core.Contains(name))
                .Where(name =>
                    (
                        name.StartsWith(prefix!, StringComparison.OrdinalIgnoreCase)
                        && !longer.Any(x => name.StartsWith(x, StringComparison.OrdinalIgnoreCase))
                    )
                    || (
                        !historyShared
                        && string.Equals(name, history, StringComparison.OrdinalIgnoreCase)
                    )
                )
                .Order(StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static async Task<string> CreateStatementAsync(
        DbConnection connection,
        string table,
        string quoted,
        bool sqlite,
        CancellationToken ct
    )
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);

        command.CommandText = sqlite
            ? $"SELECT sql FROM sqlite_master WHERE type = 'table' AND name = '{table.Replace("'", "''")}'"
            : $"SHOW CREATE TABLE {quoted}";

        var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await using var readerScope = reader.ConfigureAwait(false);

        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException($"The table {table} has no definition to export.");

        return reader.GetString(sqlite ? 0 : 1);
    }

    private static async Task WriteRowsAsync(
        DbConnection connection,
        string quoted,
        bool sqlite,
        TextWriter writer,
        CancellationToken ct
    )
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);

        command.CommandText = $"SELECT * FROM {quoted}";

        var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await using var readerScope = reader.ConfigureAwait(false);

        var columns = string.Join(
            ", ",
            Enumerable.Range(0, reader.FieldCount).Select(i => Quote(reader.GetName(i), sqlite))
        );
        var values = new string[reader.FieldCount];

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            for (var i = 0; i < values.Length; i++)
                values[i] = Literal(reader.GetValue(i), sqlite);

            await writer
                .WriteLineAsync(
                    $"INSERT INTO {quoted} ({columns}) VALUES ({string.Join(", ", values)});"
                )
                .ConfigureAwait(false);
        }
    }

    private static string Literal(object value, bool sqlite) =>
        value switch
        {
            DBNull => "NULL",
            bool b => b ? "1" : "0",
            byte[] bytes => $"X'{Convert.ToHexString(bytes)}'",
            DateTime d => $"'{d:yyyy-MM-dd HH:mm:ss.ffffff}'",
            DateTimeOffset d => $"'{d.UtcDateTime:yyyy-MM-dd HH:mm:ss.ffffff}'",
            string or char or Guid or TimeSpan => Text(
                Convert.ToString(value, CultureInfo.InvariantCulture)!,
                sqlite
            ),
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _ => Text(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", sqlite),
        };

    /// <summary>A text literal: quotes doubled, and for MySQL backslashes too, its escape.</summary>
    private static string Text(string value, bool sqlite) =>
        "'" + (sqlite ? value : value.Replace(@"\", @"\\")).Replace("'", "''") + "'";

    private static bool IsSqlite(TurboDbContext db) =>
        db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    private static string Quote(TurboDbContext db, string name) => Quote(name, IsSqlite(db));

    private static string Quote(string name, bool sqlite) =>
        sqlite ? $"\"{name.Replace("\"", "\"\"")}\"" : $"`{name.Replace("`", "``")}`";
}
