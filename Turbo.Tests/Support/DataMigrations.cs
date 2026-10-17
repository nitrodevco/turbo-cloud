using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Turbo.Tests.Support;

/// <summary>Runs a data-only migration's SQL on an in-memory SQLite table shaped like the real one.</summary>
internal static class DataMigrations
{
    public static void Run(SqliteConnection connection, Migration migration, bool up)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
        typeof(Migration)
            .GetMethod(
                up ? "Up" : "Down",
                BindingFlags.Instance | BindingFlags.NonPublic,
                [typeof(MigrationBuilder)]
            )!
            .Invoke(migration, [builder]);

        foreach (var sql in builder.Operations.OfType<SqlOperation>())
            Exec(connection, sql.Sql.Replace('`', '"'));
    }

    public static void Exec(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public static List<string> Column(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<string>();

        while (reader.Read())
            rows.Add(reader.GetString(0));

        return rows;
    }
}
