using System.Reflection;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Turbo.Database.Migrations;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// Until 2026-10-13 a <c>:gift</c> ending in a yes/no flag gave its word as a badge, so players
/// hold badges named <c>false</c> (an empty slot, <c>badge_name_false</c>). The migration takes
/// those away, and only those.
/// </summary>
public sealed class FalseBadgeCleanupTests
{
    [Fact]
    public void TheMigration_TakesAwayTheFalseAndTrueBadges_AndNothingElse()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Exec(
            connection,
            "CREATE TABLE player_badges (player_id INTEGER, badge_code TEXT, slot_id INTEGER);"
                + "INSERT INTO player_badges VALUES (1, 'false', 1), (1, 'ADM', 2), (2, 'true', 0), (2, 'ACH_Login1', 0);"
                + "CREATE TABLE badge_definitions (badge_code TEXT, rarity INTEGER);"
                + "INSERT INTO badge_definitions VALUES ('false', NULL), ('ADM', 4);"
        );

        Up(connection);
        Up(connection);

        Rows(connection, "SELECT player_id || ':' || badge_code FROM player_badges ORDER BY 1;")
            .Should()
            .Equal("1:ADM", "2:ACH_Login1");
        Rows(connection, "SELECT badge_code FROM badge_definitions ORDER BY 1;")
            .Should()
            .Equal("ADM");
    }

    private static void Up(SqliteConnection connection)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
        typeof(Migration)
            .GetMethod(
                "Up",
                BindingFlags.Instance | BindingFlags.NonPublic,
                [typeof(MigrationBuilder)]
            )!
            .Invoke(new RemoveFalseBadges(), [builder]);

        foreach (var sql in builder.Operations.OfType<SqlOperation>())
            Exec(connection, sql.Sql.Replace('`', '"'));
    }

    private static void Exec(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static List<string> Rows(SqliteConnection connection, string sql)
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
