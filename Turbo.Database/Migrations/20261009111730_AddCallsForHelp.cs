using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCallsForHelp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "cfh_reports",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        reporter_id = table.Column<int>(type: "int", nullable: false),
                        reported_id = table.Column<int>(type: "int", nullable: true),
                        room_id = table.Column<int>(type: "int", nullable: true),
                        topic_id = table.Column<int>(type: "int", nullable: false),
                        message = table
                            .Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        reporter_name = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        reporter_email = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        closed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        sanctioned = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        auto_moderated = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        appeal_status = table.Column<byte>(
                            type: "tinyint unsigned",
                            nullable: false,
                            defaultValue: (byte)0
                        ),
                        appeal_created_at = table.Column<DateTime>(
                            type: "datetime(6)",
                            nullable: true
                        ),
                        appeal_resolved_at = table.Column<DateTime>(
                            type: "datetime(6)",
                            nullable: true
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
                        table.PrimaryKey("PK_cfh_reports", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "cfh_report_chat_lines",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        report_id = table.Column<int>(type: "int", nullable: false),
                        position = table.Column<int>(type: "int", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        text = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_cfh_report_chat_lines", x => x.id);
                        table.ForeignKey(
                            name: "FK_cfh_report_chat_lines_cfh_reports_report_id",
                            column: x => x.report_id,
                            principalTable: "cfh_reports",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_cfh_report_chat_lines_report_id_position",
                table: "cfh_report_chat_lines",
                columns: new[] { "report_id", "position" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_cfh_reports_reporter_id_closed_at",
                table: "cfh_reports",
                columns: new[] { "reporter_id", "closed_at" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "cfh_report_chat_lines");

            migrationBuilder.DropTable(name: "cfh_reports");
        }
    }
}
