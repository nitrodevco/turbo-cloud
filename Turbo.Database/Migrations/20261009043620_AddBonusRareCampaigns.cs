using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBonusRareCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "rewards_received",
                table: "player_bonus_rare_progress",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder
                .CreateTable(
                    name: "bonus_rare_campaigns",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        code = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        furniture_name = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        product_code = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        credits_required = table.Column<int>(type: "int", nullable: false),
                        source = table.Column<int>(type: "int", nullable: false),
                        starts_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        ends_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_bonus_rare_campaigns", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "bonus_rare_receipts",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        reference = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        campaign_code = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        credits = table.Column<int>(type: "int", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_bonus_rare_receipts", x => x.id);
                        table.ForeignKey(
                            name: "FK_bonus_rare_receipts_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_bonus_rare_campaigns_code",
                table: "bonus_rare_campaigns",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_bonus_rare_campaigns_starts_at",
                table: "bonus_rare_campaigns",
                column: "starts_at"
            );

            migrationBuilder.CreateIndex(
                name: "IX_bonus_rare_receipts_player_id",
                table: "bonus_rare_receipts",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_bonus_rare_receipts_reference",
                table: "bonus_rare_receipts",
                column: "reference",
                unique: true
            );

            // The campaign the server ran from Turbo:Catalog:BonusRare's defaults, kept running:
            // its progress is under the same code, and bought credits still count as before.
            migrationBuilder.InsertData(
                table: "bonus_rare_campaigns",
                columns:
                [
                    "code",
                    "furniture_name",
                    "product_code",
                    "credits_required",
                    "source",
                    "starts_at",
                    "ends_at",
                ],
                values: new object[]
                {
                    "bonusbag26_3",
                    "bonusbag26_3",
                    "bonusbag26_3",
                    120,
                    0,
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    null,
                }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "bonus_rare_campaigns");

            migrationBuilder.DropTable(name: "bonus_rare_receipts");

            migrationBuilder.DropColumn(
                name: "rewards_received",
                table: "player_bonus_rare_progress"
            );
        }
    }
}
