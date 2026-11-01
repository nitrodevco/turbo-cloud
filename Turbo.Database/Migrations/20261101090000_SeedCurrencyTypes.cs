using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Seeds <c>currency_types</c> with the three currencies the client always draws in the purse:
    /// credits, duckets (activity point type 0) and diamonds (activity point type 5). Every wallet
    /// credit, every catalog price and <c>:give</c> name a row of this table, and a hotel installed
    /// from nothing had none: the purse showed zeros, the catalog could charge nothing and
    /// <c>:give</c> knew no currency, with nothing in the panel saying why. A row is added only
    /// when the hotel has no currency of that kind (type and activity point type), whatever it
    /// named it, so a hotel's own rows stay; staff rename, disable or add others on the Content
    /// page.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261101090000_SeedCurrencyTypes")]
    public partial class SeedCurrencyTypes : Migration
    {
        /// <summary>Name, <c>CurrencyType</c> (1 credits, 4 activity points), activity point type.</summary>
        private static readonly (string Name, int Type, int? ActivityPointType)[] CURRENCIES =
        [
            ("credits", 1, null),
            ("duckets", 4, 0),
            ("diamonds", 4, 5),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, type, activityPointType) in CURRENCIES)
                migrationBuilder.Sql(Insert(name, type, activityPointType));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seeded rows are indistinguishable from operator rows once edited; they stay.
        }

        /// <summary>
        /// The row, unless the hotel has one of its kind. <c>FROM (SELECT 1)</c> rather than
        /// <c>FROM DUAL</c>, and <c>CURRENT_TIMESTAMP</c> spelt out, so the statement also runs on
        /// the SQLite the tests use, whose schema has no default for <c>created_at</c>.
        /// </summary>
        private static string Insert(string name, int type, int? activityPointType)
        {
            var point = activityPointType?.ToString() ?? "NULL";
            var sameKind = activityPointType is null
                ? "`activity_point_type` IS NULL"
                : $"`activity_point_type` = {point}";

            return $"INSERT INTO `currency_types` (`name`, `type`, `activity_point_type`, `enabled`, `created_at`) "
                + $"SELECT '{name}', {type}, {point}, 1, CURRENT_TIMESTAMP FROM (SELECT 1) AS seed "
                + $"WHERE NOT EXISTS (SELECT 1 FROM `currency_types` WHERE `type` = {type} AND {sameKind});";
        }
    }
}
