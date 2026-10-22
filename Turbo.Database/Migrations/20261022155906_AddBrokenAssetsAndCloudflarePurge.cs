using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokenAssetsAndCloudflarePurge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AddColumn<string>(
                    name: "purge_error",
                    table: "asset_publishes",
                    type: "varchar(512)",
                    maxLength: 512,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "purged",
                table: "asset_publishes",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<byte[]>(
                name: "cloudflare_token",
                table: "asset_publish_targets",
                type: "longblob",
                nullable: true
            );

            migrationBuilder
                .AddColumn<string>(
                    name: "cloudflare_zone_id",
                    table: "asset_publish_targets",
                    type: "varchar(64)",
                    maxLength: 64,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "asset_broken_ignores",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        source = table
                            .Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        asset_key = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        note = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_asset_broken_ignores", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "asset_broken_reports",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        url = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        status = table.Column<int>(type: "int", nullable: false),
                        reports = table.Column<int>(type: "int", nullable: false),
                        issue_url = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        last_seen_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_asset_broken_reports", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_asset_broken_ignores_source_asset_key",
                table: "asset_broken_ignores",
                columns: new[] { "source", "asset_key" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_asset_broken_reports_url",
                table: "asset_broken_reports",
                column: "url",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "asset_broken_ignores");

            migrationBuilder.DropTable(name: "asset_broken_reports");

            migrationBuilder.DropColumn(name: "purge_error", table: "asset_publishes");

            migrationBuilder.DropColumn(name: "purged", table: "asset_publishes");

            migrationBuilder.DropColumn(name: "cloudflare_token", table: "asset_publish_targets");

            migrationBuilder.DropColumn(name: "cloudflare_zone_id", table: "asset_publish_targets");
        }
    }
}
