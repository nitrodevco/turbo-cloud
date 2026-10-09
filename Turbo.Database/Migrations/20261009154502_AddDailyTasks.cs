using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Daily task definitions and the tasks given to players. The seed holds only official tasks
    /// (codes from the hotel's <c>dailytask.&lt;code&gt;.*</c> texts):
    /// <list type="bullet">
    /// <item>1734011389107_G "Backpackers Delight", enabled: an explore task of 10 rooms. Its name
    /// and completion text are those of the older <c>quests.daily.EXPLORE</c>, whose hint says
    /// "you must explore 10 different rooms". Its reward is not shown anywhere; 10 duckets is the
    /// amount most official tasks show (inference).</item>
    /// <item>The three tasks of the official daily tasks capture (2026-10-09), with the duckets it
    /// shows: 1735033170628_G "Ramped Up" x10, 1736419911400_G "I, Spider" x20 and
    /// 1742379817871_G "Rest Your Feet!" x10. Their hints name a skate ramp, a spider and a bed,
    /// but not which furni, so they are disabled with no target until a hotel names them.</item>
    /// </list>
    /// INSERT IGNORE on the code, so a hotel's own rows stay.
    /// </summary>
    public partial class AddDailyTasks : Migration
    {
        private const string SEED =
            "INSERT IGNORE INTO daily_task_definitions (code, quest_type, target, required_repeats, image_version, catalog_name, is_bonus, enabled, reward_product_type, reward_type_id, reward_amount, created_at) VALUES "
            + "('1734011389107_G', 'explore', '', 10, '', '', 0, 1, 8, '0', 10, UTC_TIMESTAMP()), "
            + "('1735033170628_G', 'find_furni', '', 1, '', '', 0, 0, 8, '0', 10, UTC_TIMESTAMP()), "
            + "('1736419911400_G', 'find_furni', '', 1, '', '', 0, 0, 8, '0', 20, UTC_TIMESTAMP()), "
            + "('1742379817871_G', 'find_furni', '', 1, '', '', 0, 0, 8, '0', 10, UTC_TIMESTAMP());";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "daily_task_definitions",
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
                        quest_type = table
                            .Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        target = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        required_repeats = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 1
                        ),
                        image_version = table
                            .Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        catalog_name = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        is_bonus = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        enabled = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: true
                        ),
                        reward_product_type = table.Column<short>(
                            type: "smallint",
                            nullable: false,
                            defaultValue: (short)8
                        ),
                        reward_type_id = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        reward_amount = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_daily_task_definitions", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "player_daily_tasks",
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
                        day_starts_at = table.Column<DateTime>(
                            type: "datetime(6)",
                            nullable: false
                        ),
                        repeats = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                        counted = table
                            .Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        status = table.Column<byte>(
                            type: "tinyint unsigned",
                            nullable: false,
                            defaultValue: (byte)0
                        ),
                        completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        claimed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_player_daily_tasks", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_daily_tasks_daily_task_definitions_definition_id",
                            column: x => x.definition_id,
                            principalTable: "daily_task_definitions",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_player_daily_tasks_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_daily_task_definitions_code",
                table: "daily_task_definitions",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_daily_tasks_definition_id",
                table: "player_daily_tasks",
                column: "definition_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_daily_tasks_player_id_day_starts_at",
                table: "player_daily_tasks",
                columns: new[] { "player_id", "day_starts_at" }
            );

            migrationBuilder.Sql(SEED);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "player_daily_tasks");

            migrationBuilder.DropTable(name: "daily_task_definitions");
        }
    }
}
