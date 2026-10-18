using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Reward tracks move from configuration into tables of their own (tracks with their theme,
    /// premium and dates; tasks, task levels and prizes), so staff can change them in the panel
    /// without a restart. Habbo's Introduction and Habboween 2026 tracks are put in as Habbo sent
    /// them, unless a track of that id is already there; a track the hotel configured under
    /// <c>Turbo:RewardTracks:Tracks</c> is moved in by the server when it starts.
    /// </summary>
    public partial class AddRewardTrackTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "reward_tracks",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        track_id = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        theme = table
                            .Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        enabled = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: true
                        ),
                        starts_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        ends_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        sort_order = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        has_premium = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        premium_task_points_boost = table.Column<double>(
                            type: "double",
                            nullable: false,
                            defaultValue: 1.0
                        ),
                        premium_instant_points = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        premium_cost_credits = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        premium_cost_diamonds = table.Column<int>(
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
                        table.PrimaryKey("PK_reward_tracks", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "reward_track_prizes",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        reward_track_id = table.Column<int>(type: "int", nullable: false),
                        prize_id = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        required_points = table.Column<int>(type: "int", nullable: false),
                        product_type = table.Column<short>(type: "smallint", nullable: false),
                        reward_type_id = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        extra_params = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        amount = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                        premium = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        furniture_definition_id = table.Column<int>(type: "int", nullable: true),
                        name = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        sort_order = table.Column<int>(
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
                        table.PrimaryKey("PK_reward_track_prizes", x => x.id);
                        table.ForeignKey(
                            name: "FK_reward_track_prizes_reward_tracks_reward_track_id",
                            column: x => x.reward_track_id,
                            principalTable: "reward_tracks",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "reward_track_tasks",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        reward_track_id = table.Column<int>(type: "int", nullable: false),
                        task_id = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        action_type = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        parameter = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        premium = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        sort_order = table.Column<int>(
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
                        table.PrimaryKey("PK_reward_track_tasks", x => x.id);
                        table.ForeignKey(
                            name: "FK_reward_track_tasks_reward_tracks_reward_track_id",
                            column: x => x.reward_track_id,
                            principalTable: "reward_tracks",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "reward_track_task_levels",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        reward_track_task_id = table.Column<int>(type: "int", nullable: false),
                        required_count = table.Column<int>(type: "int", nullable: false),
                        points = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                        premium = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
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
                        table.PrimaryKey("PK_reward_track_task_levels", x => x.id);
                        table.ForeignKey(
                            name: "FK_reward_track_task_levels_reward_track_tasks_reward_track_tas~",
                            column: x => x.reward_track_task_id,
                            principalTable: "reward_track_tasks",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_reward_track_prizes_reward_track_id_prize_id",
                table: "reward_track_prizes",
                columns: new[] { "reward_track_id", "prize_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_reward_track_task_levels_reward_track_task_id_required_count",
                table: "reward_track_task_levels",
                columns: new[] { "reward_track_task_id", "required_count" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_reward_track_tasks_reward_track_id_task_id",
                table: "reward_track_tasks",
                columns: new[] { "reward_track_id", "task_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_reward_tracks_track_id",
                table: "reward_tracks",
                column: "track_id",
                unique: true
            );

            for (var i = 0; i < HABBO_TRACKS.Length; i++)
                foreach (var sql in HABBO_TRACKS[i].Sql(i))
                    migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "reward_track_prizes");

            migrationBuilder.DropTable(name: "reward_track_task_levels");

            migrationBuilder.DropTable(name: "reward_track_tasks");

            migrationBuilder.DropTable(name: "reward_tracks");
        }

        private sealed record SeedPremium(
            double TaskPointsBoost,
            int InstantPoints,
            int CostCredits,
            int CostDiamonds
        );

        private sealed record SeedTask(
            string Id,
            string ActionType,
            (int Count, int Points)[] Levels
        );

        private sealed record SeedPrize(
            string Id,
            int RequiredPoints,
            short ProductType,
            string RewardTypeId,
            string ExtraParams,
            int Amount,
            bool Premium
        );

        /// <summary>A seeded track: each row is added only when it is not there yet.</summary>
        private sealed record SeedTrack(
            string Id,
            string Theme,
            SeedPremium Premium,
            SeedTask[] Tasks,
            SeedPrize[] Prizes
        )
        {
            public IEnumerable<string> Sql(int sortOrder)
            {
                var track = Text(Id);
                var boost = Premium.TaskPointsBoost.ToString(CultureInfo.InvariantCulture);

                yield return $"INSERT INTO `reward_tracks` (`track_id`, `theme`, `enabled`, `sort_order`, `has_premium`, `premium_task_points_boost`, `premium_instant_points`, `premium_cost_credits`, `premium_cost_diamonds`, `created_at`) SELECT {track}, {Text(Theme)}, 1, {sortOrder}, 1, {boost}, {Premium.InstantPoints}, {Premium.CostCredits}, {Premium.CostDiamonds}, CURRENT_TIMESTAMP FROM (SELECT 1) AS `one` WHERE NOT EXISTS (SELECT 1 FROM `reward_tracks` WHERE `track_id` = {track});";

                for (var i = 0; i < Tasks.Length; i++)
                {
                    var task = Tasks[i];
                    var taskId = Text(task.Id);

                    yield return $"INSERT INTO `reward_track_tasks` (`reward_track_id`, `task_id`, `action_type`, `parameter`, `premium`, `sort_order`, `created_at`) SELECT t.`id`, {taskId}, {Text(task.ActionType)}, '', 0, {i}, CURRENT_TIMESTAMP FROM `reward_tracks` t WHERE t.`track_id` = {track} AND NOT EXISTS (SELECT 1 FROM `reward_track_tasks` k WHERE k.`reward_track_id` = t.`id` AND k.`task_id` = {taskId});";

                    foreach (var (count, points) in task.Levels)
                        yield return $"INSERT INTO `reward_track_task_levels` (`reward_track_task_id`, `required_count`, `points`, `premium`, `created_at`) SELECT k.`id`, {count}, {points}, 0, CURRENT_TIMESTAMP FROM `reward_track_tasks` k JOIN `reward_tracks` t ON t.`id` = k.`reward_track_id` WHERE t.`track_id` = {track} AND k.`task_id` = {taskId} AND NOT EXISTS (SELECT 1 FROM `reward_track_task_levels` l WHERE l.`reward_track_task_id` = k.`id` AND l.`required_count` = {count});";
                }

                for (var i = 0; i < Prizes.Length; i++)
                {
                    var prize = Prizes[i];
                    var prizeId = Text(prize.Id);

                    yield return $"INSERT INTO `reward_track_prizes` (`reward_track_id`, `prize_id`, `required_points`, `product_type`, `reward_type_id`, `extra_params`, `amount`, `premium`, `furniture_definition_id`, `name`, `sort_order`, `created_at`) SELECT t.`id`, {prizeId}, {prize.RequiredPoints}, {prize.ProductType}, {Text(prize.RewardTypeId)}, {Text(prize.ExtraParams)}, {prize.Amount}, {(prize.Premium ? 1 : 0)}, NULL, '', {i}, CURRENT_TIMESTAMP FROM `reward_tracks` t WHERE t.`track_id` = {track} AND NOT EXISTS (SELECT 1 FROM `reward_track_prizes` p WHERE p.`reward_track_id` = t.`id` AND p.`prize_id` = {prizeId});";
                }
            }

            private static string Text(string value) =>
                $"'{value.Replace("\\", "\\\\").Replace("'", "''")}'";
        }
    }
}
