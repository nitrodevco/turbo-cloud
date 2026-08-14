using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddLtdRaffleSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "unique_remaining", table: "catalog_products");

            migrationBuilder.DropColumn(name: "unique_size", table: "catalog_products");

            migrationBuilder
                .CreateTable(
                    name: "ltd_series",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        catalog_product_id = table.Column<int>(type: "int", nullable: false),
                        total_quantity = table.Column<int>(type: "int", nullable: false),
                        remaining_quantity = table.Column<int>(type: "int", nullable: false),
                        raffle_window_seconds = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 30
                        ),
                        is_active = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: true
                        ),
                        has_raffle_finished = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        starts_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        ends_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        updated_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                        deleted_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: true)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_ltd_series", x => x.id);
                        table.ForeignKey(
                            name: "FK_ltd_series_catalog_products_catalog_product_id",
                            column: x => x.catalog_product_id,
                            principalTable: "catalog_products",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "ltd_raffle_entries",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        series_id = table.Column<int>(type: "int", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        batch_id = table
                            .Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        entered_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        result = table
                            .Column<string>(
                                type: "varchar(20)",
                                maxLength: 20,
                                nullable: false,
                                defaultValue: "pending"
                            )
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        serial_number = table.Column<int>(type: "int", nullable: true),
                        processed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        updated_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                        deleted_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: true)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_ltd_raffle_entries", x => x.id);
                        table.ForeignKey(
                            name: "FK_ltd_raffle_entries_ltd_series_series_id",
                            column: x => x.series_id,
                            principalTable: "ltd_series",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_ltd_raffle_entries_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ltd_raffle_entries_player_id",
                table: "ltd_raffle_entries",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ltd_raffle_entries_series_id",
                table: "ltd_raffle_entries",
                column: "series_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ltd_series_catalog_product_id",
                table: "ltd_series",
                column: "catalog_product_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ltd_raffle_entries");

            migrationBuilder.DropTable(name: "ltd_series");

            migrationBuilder.AddColumn<int>(
                name: "unique_remaining",
                table: "catalog_products",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "unique_size",
                table: "catalog_products",
                type: "int",
                nullable: false,
                defaultValue: 0
            );
        }
    }
}
