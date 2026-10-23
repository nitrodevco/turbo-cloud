using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddGamePlayerResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "game_player_results",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        game_type_id = table.Column<int>(type: "int", nullable: false),
                        game_id = table.Column<int>(type: "int", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        team_id = table.Column<int>(type: "int", nullable: false),
                        score = table.Column<int>(type: "int", nullable: false),
                        placement = table.Column<int>(type: "int", nullable: false),
                        year = table.Column<int>(type: "int", nullable: false),
                        week = table.Column<int>(type: "int", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_game_player_results", x => x.id);
                        table.ForeignKey(
                            name: "FK_game_player_results_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_game_player_results_game_type_id_game_id_player_id",
                table: "game_player_results",
                columns: new[] { "game_type_id", "game_id", "player_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_game_player_results_game_type_id_year_week",
                table: "game_player_results",
                columns: new[] { "game_type_id", "year", "week" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_game_player_results_player_id_game_type_id",
                table: "game_player_results",
                columns: new[] { "player_id", "game_type_id" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "game_player_results");
        }
    }
}
