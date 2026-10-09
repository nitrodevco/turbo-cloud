using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "community_goals",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        code = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        mode = table.Column<int>(type: "int", nullable: false),
                        starts_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        ends_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        level_scores = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        reward_ranks = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        side_one_page_id = table.Column<int>(type: "int", nullable: true),
                        side_two_page_id = table.Column<int>(type: "int", nullable: true),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_community_goals", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "community_goal_contributions",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        goal_id = table.Column<int>(type: "int", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        side_one = table.Column<int>(type: "int", nullable: false),
                        side_two = table.Column<int>(type: "int", nullable: false),
                        score = table.Column<int>(type: "int", nullable: false),
                        voted_side = table.Column<int>(type: "int", nullable: true),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_community_goal_contributions", x => x.id);
                        table.ForeignKey(
                            name: "FK_community_goal_contributions_community_goals_goal_id",
                            column: x => x.goal_id,
                            principalTable: "community_goals",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_community_goal_contributions_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_community_goal_contributions_goal_id_player_id",
                table: "community_goal_contributions",
                columns: new[] { "goal_id", "player_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_community_goal_contributions_goal_id_score",
                table: "community_goal_contributions",
                columns: new[] { "goal_id", "score" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_community_goal_contributions_player_id",
                table: "community_goal_contributions",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_community_goals_code",
                table: "community_goals",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_community_goals_starts_at",
                table: "community_goals",
                column: "starts_at"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "community_goal_contributions");

            migrationBuilder.DropTable(name: "community_goals");
        }
    }
}
