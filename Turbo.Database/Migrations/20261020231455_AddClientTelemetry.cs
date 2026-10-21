using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// What clients report about themselves, kept for the admin panel's Client health and Usage
    /// pages: performance, latency, chat lag, logged events and login steps (raw, expiring), and
    /// their daily figures. <c>performance_logs</c>, which nothing writes and which has an address
    /// column the new tables leave out, stays until it can be dropped: dropping a table needs
    /// <c>AllowDestructiveMigrations</c> for a start.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261020231455_AddClientTelemetry")]
    public partial class AddClientTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "client_event_logs",
                    columns: table => new
                    {
                        id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        room_id = table.Column<int>(type: "int", nullable: true),
                        category = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        type = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        action = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        extra_string = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        extra_int = table.Column<int>(type: "int", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_client_event_logs", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "client_lag_warnings",
                    columns: table => new
                    {
                        id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        room_id = table.Column<int>(type: "int", nullable: true),
                        warning_count = table.Column<int>(type: "int", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_client_lag_warnings", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "client_latency_reports",
                    columns: table => new
                    {
                        id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        room_id = table.Column<int>(type: "int", nullable: true),
                        average_ms = table.Column<int>(type: "int", nullable: false),
                        valid_average_ms = table.Column<int>(type: "int", nullable: false),
                        ping_count = table.Column<int>(type: "int", nullable: false),
                        browser_family = table
                            .Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        os_family = table
                            .Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        device_type = table
                            .Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_client_latency_reports", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "client_login_steps",
                    columns: table => new
                    {
                        id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        session = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        player_id = table.Column<int>(type: "int", nullable: true),
                        step = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        ms_since_connect = table.Column<int>(type: "int", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_client_login_steps", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "client_performance_logs",
                    columns: table => new
                    {
                        id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        room_id = table.Column<int>(type: "int", nullable: true),
                        elapsed_seconds = table.Column<int>(type: "int", nullable: false),
                        user_agent = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        flash_version = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        os = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        browser = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        browser_family = table
                            .Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        os_family = table
                            .Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        device_type = table
                            .Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        is_debugger = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        memory_kb = table.Column<int>(type: "int", nullable: false),
                        garbage_collections = table.Column<int>(type: "int", nullable: false),
                        average_update_interval_ms = table.Column<int>(
                            type: "int",
                            nullable: false
                        ),
                        slow_updates = table.Column<int>(type: "int", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_client_performance_logs", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "client_telemetry_daily",
                    columns: table => new
                    {
                        id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        day = table.Column<DateOnly>(type: "date", nullable: false),
                        kind = table
                            .Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        key1 = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        key2 = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        key3 = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        samples = table.Column<int>(type: "int", nullable: false),
                        players = table.Column<int>(type: "int", nullable: false),
                        sum = table.Column<long>(type: "bigint", nullable: false),
                        p50 = table.Column<int>(type: "int", nullable: false),
                        p95 = table.Column<int>(type: "int", nullable: false),
                        max = table.Column<int>(type: "int", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_client_telemetry_daily", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_client_event_logs_category_created_at",
                table: "client_event_logs",
                columns: new[] { "category", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_event_logs_created_at",
                table: "client_event_logs",
                column: "created_at"
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_event_logs_player_id_created_at",
                table: "client_event_logs",
                columns: new[] { "player_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_event_logs_room_id_created_at",
                table: "client_event_logs",
                columns: new[] { "room_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_lag_warnings_created_at",
                table: "client_lag_warnings",
                column: "created_at"
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_lag_warnings_player_id_created_at",
                table: "client_lag_warnings",
                columns: new[] { "player_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_lag_warnings_room_id_created_at",
                table: "client_lag_warnings",
                columns: new[] { "room_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_latency_reports_created_at",
                table: "client_latency_reports",
                column: "created_at"
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_latency_reports_player_id_created_at",
                table: "client_latency_reports",
                columns: new[] { "player_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_latency_reports_room_id_created_at",
                table: "client_latency_reports",
                columns: new[] { "room_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_login_steps_created_at",
                table: "client_login_steps",
                column: "created_at"
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_login_steps_session",
                table: "client_login_steps",
                column: "session"
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_performance_logs_created_at",
                table: "client_performance_logs",
                column: "created_at"
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_performance_logs_player_id_created_at",
                table: "client_performance_logs",
                columns: new[] { "player_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_performance_logs_room_id_created_at",
                table: "client_performance_logs",
                columns: new[] { "room_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_client_telemetry_daily_day_kind_key1_key2_key3",
                table: "client_telemetry_daily",
                columns: new[] { "day", "kind", "key1", "key2", "key3" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "client_event_logs");

            migrationBuilder.DropTable(name: "client_lag_warnings");

            migrationBuilder.DropTable(name: "client_latency_reports");

            migrationBuilder.DropTable(name: "client_login_steps");

            migrationBuilder.DropTable(name: "client_performance_logs");

            migrationBuilder.DropTable(name: "client_telemetry_daily");
        }
    }
}
