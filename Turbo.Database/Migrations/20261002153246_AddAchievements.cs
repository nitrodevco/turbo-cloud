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
                    name: "achievement_awards",
                    columns: table => new
                    {
                        PlayerId = table.Column<int>(type: "int", nullable: false),
                        AchievementId = table.Column<int>(type: "int", nullable: false),
                        Level = table.Column<int>(type: "int", nullable: false),
                        Revision = table.Column<int>(type: "int", nullable: false),
                        AwardKey = table
                            .Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        DefinitionJson = table
                            .Column<string>(
                                type: "longtext",
                                maxLength: 2147483647,
                                nullable: false
                            )
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        RewardJson = table
                            .Column<string>(
                                type: "longtext",
                                maxLength: 2147483647,
                                nullable: false
                            )
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        EarnedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        Completed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        Presented = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        DeliveredRewards = table.Column<int>(type: "int", nullable: false),
                        BlockedReason = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey(
                            "PK_achievement_awards",
                            x => new
                            {
                                x.PlayerId,
                                x.AchievementId,
                                x.Level,
                            }
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "achievement_badge_entitlements",
                    columns: table => new
                    {
                        PlayerId = table.Column<int>(type: "int", nullable: false),
                        AchievementId = table.Column<int>(type: "int", nullable: false),
                        BadgeCode = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Level = table.Column<int>(type: "int", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey(
                            "PK_achievement_badge_entitlements",
                            x => new { x.PlayerId, x.AchievementId }
                        );
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
                    name: "achievement_membership_intervals",
                    columns: table => new
                    {
                        Id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        PlayerId = table.Column<int>(type: "int", nullable: false),
                        StartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        EndUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        Purchased = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_achievement_membership_intervals", x => x.Id);
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
                        DistinctCount = table.Column<int>(type: "int", nullable: false),
                        IntervalsJson = table
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
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_achievement_projections", x => x.PlayerId);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "achievement_state_values",
                    columns: table => new
                    {
                        PlayerId = table.Column<int>(type: "int", nullable: false),
                        Source = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Value = table.Column<long>(type: "bigint", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey(
                            "PK_achievement_state_values",
                            x => new { x.PlayerId, x.Source }
                        );
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

            migrationBuilder
                .CreateTable(
                    name: "human_respect_operations",
                    columns: table => new
                    {
                        OperationId = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        ActorId = table.Column<int>(type: "int", nullable: false),
                        TargetId = table.Column<int>(type: "int", nullable: false),
                        Rejected = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        Completed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        ResultTotal = table.Column<int>(type: "int", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_human_respect_operations", x => x.OperationId);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "human_respect_participant_receipts",
                    columns: table => new
                    {
                        PlayerId = table.Column<int>(type: "int", nullable: false),
                        OperationId = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Kind = table
                            .Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Accepted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        ResultTotal = table.Column<int>(type: "int", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey(
                            "PK_human_respect_participant_receipts",
                            x => new
                            {
                                x.PlayerId,
                                x.OperationId,
                                x.Kind,
                            }
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "pet_nutrition_operations",
                    columns: table => new
                    {
                        OperationId = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        RoomId = table.Column<int>(type: "int", nullable: false),
                        PetId = table.Column<int>(type: "int", nullable: false),
                        OwnerId = table.Column<int>(type: "int", nullable: false),
                        SupplierId = table.Column<int>(type: "int", nullable: false),
                        BaseNutrition = table.Column<int>(type: "int", nullable: false),
                        RequestedNutrition = table.Column<int>(type: "int", nullable: false),
                        MaxNutrition = table.Column<int>(type: "int", nullable: false),
                        NutritionAfter = table.Column<int>(type: "int", nullable: false),
                        ActualGain = table.Column<int>(type: "int", nullable: false),
                        Completed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_pet_nutrition_operations", x => x.OperationId);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "pet_respect_operations",
                    columns: table => new
                    {
                        OperationId = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        RoomId = table.Column<int>(type: "int", nullable: false),
                        ActorId = table.Column<int>(type: "int", nullable: false),
                        PetId = table.Column<int>(type: "int", nullable: false),
                        OwnerId = table.Column<int>(type: "int", nullable: false),
                        BaseRespect = table.Column<int>(type: "int", nullable: false),
                        Rejected = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        Completed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        ResultRespect = table.Column<int>(type: "int", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_pet_respect_operations", x => x.OperationId);
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
                name: "IX_achievement_awards_Completed_PlayerId",
                table: "achievement_awards",
                columns: new[] { "Completed", "PlayerId" }
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
                name: "IX_achievement_membership_intervals_PlayerId",
                table: "achievement_membership_intervals",
                column: "PlayerId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "achievement_audit");

            migrationBuilder.DropTable(name: "achievement_awards");

            migrationBuilder.DropTable(name: "achievement_badge_entitlements");

            migrationBuilder.DropTable(name: "achievement_definitions");

            migrationBuilder.DropTable(name: "achievement_distinct_values");

            migrationBuilder.DropTable(name: "achievement_facts");

            migrationBuilder.DropTable(name: "achievement_membership_intervals");

            migrationBuilder.DropTable(name: "achievement_progress");

            migrationBuilder.DropTable(name: "achievement_projections");

            migrationBuilder.DropTable(name: "achievement_state_values");

            migrationBuilder.DropTable(name: "achievement_wallet_receipts");

            migrationBuilder.DropTable(name: "human_respect_operations");

            migrationBuilder.DropTable(name: "human_respect_participant_receipts");

            migrationBuilder.DropTable(name: "pet_nutrition_operations");

            migrationBuilder.DropTable(name: "pet_respect_operations");
        }
    }
}
