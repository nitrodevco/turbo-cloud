using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// How the schema reads to someone browsing it: every table and column is lower snake_case, and
/// every table either is one of the hotel's root things (players, rooms) or starts with the
/// domain it belongs to, so a sorted table list groups by domain. Mixed casing and bare names
/// (<c>ltd_series</c>, <c>security_tickets</c>, a PascalCase <c>OperationId</c> beside a
/// <c>player_id</c>) made the old schema hard to read.
/// </summary>
public sealed partial class SchemaNamingTests
{
    private static readonly string[] Roots =
    [
        "bots",
        "furniture",
        "guilds",
        "pets",
        "players",
        "quests",
        "rooms",
        "songs",
        "vouchers",
    ];

    private static readonly string[] Domains =
    [
        "achievement_",
        "asset_",
        "auth_",
        "badge_",
        "catalog_",
        "furniture_",
        "gamedata_",
        "guild_",
        "habbo_",
        "hotel_",
        "messenger_",
        "moderation_",
        "navigator_",
        "permission_",
        "pet_",
        "player_",
        "quest_",
        "room_",
        "server_",
        "voucher_",
        "wired_",
    ];

    [GeneratedRegex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$")]
    private static partial Regex SnakeCase();

    [Fact]
    public void EveryTableAndColumnIsLowerSnakeCase()
    {
        using var db = CoreMigrationsTests.OfflineContext();

        var names = db
            .Model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null)
            .SelectMany(entity =>
            {
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!);
                return entity
                    .GetProperties()
                    .Select(property => $"{table.Name}.{property.GetColumnName(table)}")
                    .Prepend(table.Name);
            });

        names
            .Where(name => !name.Split('.').All(part => SnakeCase().IsMatch(part)))
            .Should()
            .BeEmpty();
    }

    /// <summary>
    /// A time is named for what happened (<c>created_at</c>, <c>expires_at</c>); every time is UTC,
    /// so no name says so. A day a counter was last reset for is a <c>_day</c> or <c>_date</c>.
    /// </summary>
    [Fact]
    public void EveryTimeEndsInAt()
    {
        using var db = CoreMigrationsTests.OfflineContext();

        db.Model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null)
            .SelectMany(entity =>
            {
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!);
                return entity
                    .GetProperties()
                    .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
                    .Select(property => $"{table.Name}.{property.GetColumnName(table)}");
            })
            .Where(name =>
                !name.EndsWith("_at", StringComparison.Ordinal)
                && !name.EndsWith("_day", StringComparison.Ordinal)
                && !name.EndsWith("_date", StringComparison.Ordinal)
            )
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void EveryTableIsARootOrStartsWithItsDomain()
    {
        using var db = CoreMigrationsTests.OfflineContext();

        db.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .OfType<string>()
            .Where(table => !Roots.Contains(table) && !Domains.Any(table.StartsWith))
            .Should()
            .BeEmpty("a new table is named for the domain it belongs to");
    }

    [Fact]
    public void EveryIndexAndKeyIsNamedAfterItsTable()
    {
        using var db = CoreMigrationsTests.OfflineContext();

        var names = db
            .Model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null)
            .SelectMany(entity =>
                entity
                    .GetIndexes()
                    .Select(index => (entity.GetTableName()!, index.GetDatabaseName()!))
                    .Concat(
                        entity
                            .GetForeignKeys()
                            .Select(fk => (entity.GetTableName()!, fk.GetConstraintName()!))
                    )
            );

        names
            .Where(x => !SnakeCase().IsMatch(x.Item2) || !x.Item2.Contains(x.Item1))
            .Select(x => x.Item2)
            .Should()
            .BeEmpty();
    }
}
