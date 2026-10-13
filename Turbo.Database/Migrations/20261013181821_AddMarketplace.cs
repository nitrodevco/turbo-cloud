using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "marketplace_offers",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        definition_id = table.Column<int>(type: "int", nullable: false),
                        extra_data = table
                            .Column<string>(type: "longtext", maxLength: 512, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        price = table.Column<int>(type: "int", nullable: false),
                        state = table.Column<int>(type: "int", nullable: false),
                        expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        buyer_player_id = table.Column<int>(type: "int", nullable: true),
                        sold_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        revenue = table.Column<int>(type: "int", nullable: false),
                        credits_redeemed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        history_cleared = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_marketplace_offers", x => x.id);
                        table.ForeignKey(
                            name: "FK_marketplace_offers_furniture_definitions_definition_id",
                            column: x => x.definition_id,
                            principalTable: "furniture_definitions",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_marketplace_offers_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "marketplace_tokens",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        tokens = table.Column<int>(type: "int", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_marketplace_tokens", x => x.id);
                        table.ForeignKey(
                            name: "FK_marketplace_tokens_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_offers_definition_id",
                table: "marketplace_offers",
                column: "definition_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_offers_player_id_state",
                table: "marketplace_offers",
                columns: new[] { "player_id", "state" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_offers_state_definition_id",
                table: "marketplace_offers",
                columns: new[] { "state", "definition_id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_tokens_player_id",
                table: "marketplace_tokens",
                column: "player_id",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "marketplace_offers");

            migrationBuilder.DropTable(name: "marketplace_tokens");
        }
    }
}
