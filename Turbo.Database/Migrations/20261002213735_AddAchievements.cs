using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "purchased_days_subscribed",
                table: "player_subscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder
                .CreateTable(
                    name: "achievement_audit",
                    columns: table => new
                    {
                        Id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        OperationId = table
                            .Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Actor = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Reason = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        RequestJson = table
                            .Column<string>(
                                type: "longtext",
                                maxLength: 2147483647,
                                nullable: false
                            )
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        OccurredAtUtc = table.Column<DateTime>(
                            type: "datetime(6)",
                            nullable: false
                        ),
                        BeforeJson = table
                            .Column<string>(
                                type: "longtext",
                                maxLength: 2147483647,
                                nullable: false
                            )
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        AfterJson = table
                            .Column<string>(
                                type: "longtext",
                                maxLength: 2147483647,
                                nullable: false
                            )
                            .Annotation("MySql:CharSet", "utf8mb4"),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_achievement_audit", x => x.Id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "achievement_definitions",
                    columns: table => new
                    {
                        AchievementId = table.Column<int>(type: "int", nullable: false),
                        Revision = table.Column<int>(type: "int", nullable: false),
                        DefinitionJson = table
                            .Column<string>(
                                type: "longtext",
                                maxLength: 2147483647,
                                nullable: false
                            )
                            .Annotation("MySql:CharSet", "utf8mb4"),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey(
                            "PK_achievement_definitions",
                            x => new { x.AchievementId, x.Revision }
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "achievement_distinct_values",
                    columns: table => new
                    {
                        PlayerId = table.Column<int>(type: "int", nullable: false),
                        AchievementId = table.Column<int>(type: "int", nullable: false),
                        Value = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey(
                            "PK_achievement_distinct_values",
                            x => new
                            {
                                x.PlayerId,
                                x.AchievementId,
                                x.Value,
                            }
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "achievement_facts",
                    columns: table => new
                    {
                        Id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        PlayerId = table.Column<int>(type: "int", nullable: false),
                        Source = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        OperationId = table
                            .Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        OccurredAtUtc = table.Column<DateTime>(
                            type: "datetime(6)",
                            nullable: false
                        ),
                        FactJson = table
                            .Column<string>(
                                type: "longtext",
                                maxLength: 2147483647,
                                nullable: false
                            )
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        BindingsJson = table
                            .Column<string>(
                                type: "longtext",
                                maxLength: 2147483647,
                                nullable: false
                            )
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Processed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_achievement_facts", x => x.Id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "achievement_progress",
                    columns: table => new
                    {
                        PlayerId = table.Column<int>(type: "int", nullable: false),
                        AchievementId = table.Column<int>(type: "int", nullable: false),
                        Value = table.Column<long>(type: "bigint", nullable: false),
                        ForwardAdjustment = table.Column<long>(type: "bigint", nullable: false),
                        Streak = table.Column<long>(type: "bigint", nullable: false),
                        LastDayUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        EarnedLevel = table.Column<int>(type: "int", nullable: false),
                        CompletedLevel = table.Column<int>(type: "int", nullable: false),
                        ScoreEarned = table.Column<int>(type: "int", nullable: false),
                        LastLevelAtUtc = table.Column<DateTime>(
                            type: "datetime(6)",
                            nullable: true
                        ),
                        DistinctCount = table.Column<int>(type: "int", nullable: false),
                        IntervalsJson = table
                            .Column<string>(
                                type: "longtext",
                                maxLength: 2147483647,
                                nullable: false
                            )
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        OpenAwards = table
                            .Column<string>(
                                type: "longtext",
                                maxLength: 2147483647,
                                nullable: false
                            )
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        PendingDelivery = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey(
                            "PK_achievement_progress",
                            x => new { x.PlayerId, x.AchievementId }
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "achievement_projections",
                    columns: table => new
                    {
                        PlayerId = table.Column<int>(type: "int", nullable: false),
                        Score = table.Column<int>(type: "int", nullable: false),
                        EarnedLevels = table.Column<int>(type: "int", nullable: false),
                        PublicationPending = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false
                        ),
                        ReconciledStamp = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        ObservedState = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_achievement_projections", x => x.PlayerId);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "achievement_wallet_receipts",
                    columns: table => new
                    {
                        PlayerId = table.Column<int>(type: "int", nullable: false),
                        AwardKey = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        PayloadJson = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey(
                            "PK_achievement_wallet_receipts",
                            x => new { x.PlayerId, x.AwardKey }
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_achievement_audit_OperationId",
                table: "achievement_audit",
                column: "OperationId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_achievement_facts_PlayerId_OperationId",
                table: "achievement_facts",
                columns: new[] { "PlayerId", "OperationId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_achievement_facts_PlayerId_Processed_Id",
                table: "achievement_facts",
                columns: new[] { "PlayerId", "Processed", "Id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_achievement_progress_PendingDelivery_PlayerId",
                table: "achievement_progress",
                columns: new[] { "PendingDelivery", "PlayerId" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "achievement_audit");

            migrationBuilder.DropTable(name: "achievement_definitions");

            migrationBuilder.DropTable(name: "achievement_distinct_values");

            migrationBuilder.DropTable(name: "achievement_facts");

            migrationBuilder.DropTable(name: "achievement_progress");

            migrationBuilder.DropTable(name: "achievement_projections");

            migrationBuilder.DropTable(name: "achievement_wallet_receipts");

            migrationBuilder.DropColumn(
                name: "purchased_days_subscribed",
                table: "player_subscriptions"
            );
        }
    }
}
