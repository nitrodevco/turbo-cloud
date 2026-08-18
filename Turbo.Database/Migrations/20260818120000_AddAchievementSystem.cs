using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAchievementSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "achievements",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        code = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        name = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        category = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        enabled = table
                            .Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
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
                        table.PrimaryKey("PK_achievements", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "achievement_levels",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        achievement_id = table.Column<int>(type: "int", nullable: false),
                        level = table.Column<int>(type: "int", nullable: false),
                        goal_count = table.Column<int>(type: "int", nullable: false),
                        score_reward = table
                            .Column<int>(type: "int", nullable: false, defaultValue: 0),
                        currency_type_id = table.Column<int>(type: "int", nullable: true),
                        currency_reward = table
                            .Column<int>(type: "int", nullable: false, defaultValue: 0),
                        badge_code = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
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
                        table.PrimaryKey("PK_achievement_levels", x => x.id);
                        table.ForeignKey(
                            name: "FK_achievement_levels_achievements_achievement_id",
                            column: x => x.achievement_id,
                            principalTable: "achievements",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_achievement_levels_currency_types_currency_type_id",
                            column: x => x.currency_type_id,
                            principalTable: "currency_types",
                            principalColumn: "id",
                            onDelete: ReferentialAction.SetNull
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "player_achievements",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        achievement_id = table.Column<int>(type: "int", nullable: false),
                        level = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                        progress = table
                            .Column<int>(type: "int", nullable: false, defaultValue: 0),
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
                        table.PrimaryKey("PK_player_achievements", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_achievements_achievements_achievement_id",
                            column: x => x.achievement_id,
                            principalTable: "achievements",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_player_achievements_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_achievements_code",
                table: "achievements",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_achievement_levels_achievement_id_level",
                table: "achievement_levels",
                columns: new[] { "achievement_id", "level" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_achievement_levels_currency_type_id",
                table: "achievement_levels",
                column: "currency_type_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_achievements_player_id_achievement_id",
                table: "player_achievements",
                columns: new[] { "player_id", "achievement_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_achievements_achievement_id",
                table: "player_achievements",
                column: "achievement_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "player_achievements");

            migrationBuilder.DropTable(name: "achievement_levels");

            migrationBuilder.DropTable(name: "achievements");
        }
    }
}
