using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAiConversationsAndUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "ai_conversations",
                    columns: table => new
                    {
                        id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        plugin_key = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        agent = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        agent_type = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        participant = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        messages = table
                            .Column<string>(type: "longtext", nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        pending_approval_id = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        pending = table
                            .Column<string>(type: "longtext", nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_ai_conversations", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "ai_usage",
                    columns: table => new
                    {
                        id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        plugin_key = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        agent = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        provider = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        model = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        player_id = table.Column<int>(type: "int", nullable: true),
                        conversation_id = table.Column<long>(type: "bigint", nullable: true),
                        input_tokens = table.Column<int>(type: "int", nullable: false),
                        output_tokens = table.Column<int>(type: "int", nullable: false),
                        cost = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                        duration_ms = table.Column<int>(type: "int", nullable: false),
                        outcome = table
                            .Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_ai_usage", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversations_pending_approval_id",
                table: "ai_conversations",
                column: "pending_approval_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversations_plugin_key_agent_participant_updated_at",
                table: "ai_conversations",
                columns: new[] { "plugin_key", "agent", "participant", "updated_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_created_at",
                table: "ai_usage",
                column: "created_at"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_player_id_created_at",
                table: "ai_usage",
                columns: new[] { "player_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_plugin_key_created_at",
                table: "ai_usage",
                columns: new[] { "plugin_key", "created_at" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ai_conversations");

            migrationBuilder.DropTable(name: "ai_usage");
        }
    }
}
