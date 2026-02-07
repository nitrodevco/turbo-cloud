using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Main.Migrations
{
    /// <inheritdoc />
    public partial class LinkPlayerCurrenciesToCurrencyType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "currency_type",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    currency_key = table.Column<string>(
                        type: "varchar(255)",
                        maxLength: 255,
                        nullable: false
                    ),
                    is_activity_points = table.Column<bool>(
                        type: "tinyint(1)",
                        nullable: false,
                        defaultValue: false
                    ),
                    activity_point_type = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(
                        type: "varchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    enabled = table.Column<bool>(
                        type: "tinyint(1)",
                        nullable: false,
                        defaultValue: true
                    ),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currency_type", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_catalog_offers_currency_type",
                table: "catalog_offers",
                column: "currency_type"
            );

            migrationBuilder.CreateIndex(
                name: "IX_currency_type_currency_key",
                table: "currency_type",
                column: "currency_key",
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_offers_currency_type_currency_type",
                table: "catalog_offers",
                column: "currency_type",
                principalTable: "currency_type",
                principalColumn: "id"
            );

            migrationBuilder.AddColumn<int>(
                name: "currency_type_id",
                table: "player_currencies",
                type: "int",
                nullable: true
            );

            migrationBuilder.Sql(
                """
                SET SESSION sql_mode = IF(
                    FIND_IN_SET('NO_AUTO_VALUE_ON_ZERO', @@SESSION.sql_mode),
                    @@SESSION.sql_mode,
                    CONCAT_WS(',', @@SESSION.sql_mode, 'NO_AUTO_VALUE_ON_ZERO')
                );

                INSERT INTO currency_type (id, currency_key, is_activity_points, activity_point_type, name, enabled)
                VALUES
                    (0, 'duckets', 1, 0, 'Duckets', 1),
                    (7, 'credits', 0, NULL, 'Credits', 1),
                    (1000, 'silver', 0, NULL, 'Silver', 1),
                    (1001, 'emerald', 0, NULL, 'Emerald', 1)
                ON DUPLICATE KEY UPDATE
                    currency_key = VALUES(currency_key),
                    is_activity_points = VALUES(is_activity_points),
                    activity_point_type = VALUES(activity_point_type),
                    name = VALUES(name),
                    enabled = VALUES(enabled);

                UPDATE player_currencies
                SET currency_type_id = 7
                WHERE currency_type_id IS NULL
                  AND LOWER(TRIM(type)) IN ('credit', 'credits');

                UPDATE player_currencies
                SET currency_type_id = 0
                WHERE currency_type_id IS NULL
                  AND LOWER(TRIM(type)) IN ('ducket', 'duckets', 'activitypoint_0', '0');

                UPDATE player_currencies
                SET currency_type_id = 1001
                WHERE currency_type_id IS NULL
                  AND LOWER(TRIM(type)) IN ('emerald', 'emeralds');

                UPDATE player_currencies
                SET currency_type_id = 1000
                WHERE currency_type_id IS NULL
                  AND LOWER(TRIM(type)) = 'silver';

                UPDATE player_currencies
                SET currency_type_id = CAST(SUBSTRING_INDEX(LOWER(TRIM(type)), '_', -1) AS UNSIGNED)
                WHERE currency_type_id IS NULL
                  AND LOWER(TRIM(type)) REGEXP '^activitypoint_[0-9]+$';

                UPDATE player_currencies
                SET currency_type_id = CAST(LOWER(TRIM(type)) AS UNSIGNED)
                WHERE currency_type_id IS NULL
                  AND LOWER(TRIM(type)) REGEXP '^[0-9]+$';

                UPDATE player_currencies pc
                INNER JOIN currency_type ct ON LOWER(TRIM(pc.type)) = LOWER(TRIM(ct.currency_key))
                SET pc.currency_type_id = ct.id
                WHERE pc.currency_type_id IS NULL;

                INSERT INTO currency_type (id, currency_key, is_activity_points, activity_point_type, name, enabled)
                SELECT DISTINCT
                    pc.currency_type_id AS id,
                    CASE
                        WHEN pc.currency_type_id = 0 THEN 'duckets'
                        WHEN pc.currency_type_id = 7 THEN 'credits'
                        WHEN pc.currency_type_id = 1000 THEN 'silver'
                        WHEN pc.currency_type_id = 1001 THEN 'emerald'
                        ELSE CONCAT('activitypoint_', pc.currency_type_id)
                    END AS currency_key,
                    CASE
                        WHEN pc.currency_type_id IN (7, 1000, 1001) THEN 0
                        ELSE 1
                    END AS is_activity_points,
                    CASE
                        WHEN pc.currency_type_id IN (7, 1000, 1001) THEN NULL
                        ELSE pc.currency_type_id
                    END AS activity_point_type,
                    CASE
                        WHEN pc.currency_type_id = 0 THEN 'Duckets'
                        WHEN pc.currency_type_id = 7 THEN 'Credits'
                        WHEN pc.currency_type_id = 1000 THEN 'Silver'
                        WHEN pc.currency_type_id = 1001 THEN 'Emerald'
                        ELSE CONCAT('Activity Point ', pc.currency_type_id)
                    END AS name,
                    1 AS enabled
                FROM player_currencies pc
                LEFT JOIN currency_type ct ON ct.id = pc.currency_type_id
                WHERE pc.currency_type_id IS NOT NULL
                  AND ct.id IS NULL;

                UPDATE player_currencies pc
                INNER JOIN currency_type ct ON LOWER(TRIM(pc.type)) = LOWER(TRIM(ct.currency_key))
                SET pc.currency_type_id = ct.id
                WHERE pc.currency_type_id IS NULL;
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_currencies_currency_type_id",
                table: "player_currencies",
                column: "currency_type_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_currencies_player_id_currency_type_id",
                table: "player_currencies",
                columns: ["player_id", "currency_type_id"],
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_player_currencies_currency_type_currency_type_id",
                table: "player_currencies",
                column: "currency_type_id",
                principalTable: "currency_type",
                principalColumn: "id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_player_currencies_currency_type_currency_type_id",
                table: "player_currencies"
            );

            migrationBuilder.DropIndex(
                name: "IX_player_currencies_currency_type_id",
                table: "player_currencies"
            );

            migrationBuilder.DropIndex(
                name: "IX_player_currencies_player_id_currency_type_id",
                table: "player_currencies"
            );

            migrationBuilder.DropColumn(name: "currency_type_id", table: "player_currencies");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_offers_currency_type_currency_type",
                table: "catalog_offers"
            );

            migrationBuilder.DropIndex(
                name: "IX_catalog_offers_currency_type",
                table: "catalog_offers"
            );

            migrationBuilder.DropTable(name: "currency_type");
        }
    }
}
