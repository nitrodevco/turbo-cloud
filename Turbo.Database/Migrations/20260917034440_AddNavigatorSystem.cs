using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddNavigatorSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "score",
                table: "rooms",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<bool>(
                name: "staff_pick",
                table: "rooms",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder
                .AddColumn<string>(
                    name: "tags",
                    table: "rooms",
                    type: "varchar(128)",
                    maxLength: 128,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .AlterColumn<string>(
                    name: "figure",
                    table: "players",
                    type: "varchar(279)",
                    maxLength: 279,
                    nullable: false,
                    defaultValue: "hr-115-42.hd-195-19.ch-3030-82.lg-275-1408.fa-1201.ca-1804-64",
                    oldClrType: typeof(string),
                    oldType: "varchar(100)",
                    oldMaxLength: 100,
                    oldDefaultValue: "hr-115-42.hd-195-19.ch-3030-82.lg-275-1408.fa-1201.ca-1804-64"
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "home_room_id",
                table: "player_settings",
                type: "int",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "navigator_left_pane_hidden",
                table: "player_settings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<int>(
                name: "navigator_results_mode",
                table: "player_settings",
                type: "int",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.AddColumn<int>(
                name: "navigator_window_height",
                table: "player_settings",
                type: "int",
                nullable: false,
                defaultValue: 400
            );

            migrationBuilder.AddColumn<int>(
                name: "navigator_window_width",
                table: "player_settings",
                type: "int",
                nullable: false,
                defaultValue: 425
            );

            migrationBuilder.AddColumn<int>(
                name: "navigator_window_x",
                table: "player_settings",
                type: "int",
                nullable: false,
                defaultValue: 427
            );

            migrationBuilder.AddColumn<int>(
                name: "navigator_window_y",
                table: "player_settings",
                type: "int",
                nullable: false,
                defaultValue: 41
            );

            migrationBuilder
                .CreateTable(
                    name: "player_navigator_collapsed_categories",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        search_code = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
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
                        table.PrimaryKey("PK_player_navigator_collapsed_categories", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_navigator_collapsed_categories_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "player_navigator_saved_searches",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        search_id = table.Column<int>(type: "int", nullable: false),
                        search_code = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        filter = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
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
                        table.PrimaryKey("PK_player_navigator_saved_searches", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_navigator_saved_searches_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "player_navigator_view_modes",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        search_code = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        view_mode = table.Column<int>(type: "int", nullable: false),
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
                        table.PrimaryKey("PK_player_navigator_view_modes", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_navigator_view_modes_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "room_events",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        room_id = table.Column<int>(type: "int", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        category_id = table.Column<int>(type: "int", nullable: false),
                        name = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        description = table
                            .Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
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
                        table.PrimaryKey("PK_room_events", x => x.id);
                        table.ForeignKey(
                            name: "FK_room_events_navigator_eventcats_category_id",
                            column: x => x.category_id,
                            principalTable: "navigator_eventcats",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_room_events_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_room_events_rooms_room_id",
                            column: x => x.room_id,
                            principalTable: "rooms",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "room_ratings",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        room_id = table.Column<int>(type: "int", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        rating = table.Column<int>(type: "int", nullable: false),
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
                        table.PrimaryKey("PK_room_ratings", x => x.id);
                        table.ForeignKey(
                            name: "FK_room_ratings_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_room_ratings_rooms_room_id",
                            column: x => x.room_id,
                            principalTable: "rooms",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_player_navigator_collapsed_categories_player_id_search_code",
                table: "player_navigator_collapsed_categories",
                columns: new[] { "player_id", "search_code" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_navigator_saved_searches_player_id_search_id",
                table: "player_navigator_saved_searches",
                columns: new[] { "player_id", "search_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_navigator_view_modes_player_id_search_code",
                table: "player_navigator_view_modes",
                columns: new[] { "player_id", "search_code" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_room_events_category_id",
                table: "room_events",
                column: "category_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_room_events_expires_at",
                table: "room_events",
                column: "expires_at"
            );

            migrationBuilder.CreateIndex(
                name: "IX_room_events_player_id",
                table: "room_events",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_room_events_room_id",
                table: "room_events",
                column: "room_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_room_ratings_player_id",
                table: "room_ratings",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_room_ratings_room_id_player_id",
                table: "room_ratings",
                columns: new[] { "room_id", "player_id" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "player_navigator_collapsed_categories");

            migrationBuilder.DropTable(name: "player_navigator_saved_searches");

            migrationBuilder.DropTable(name: "player_navigator_view_modes");

            migrationBuilder.DropTable(name: "room_events");

            migrationBuilder.DropTable(name: "room_ratings");

            migrationBuilder.DropColumn(name: "score", table: "rooms");

            migrationBuilder.DropColumn(name: "staff_pick", table: "rooms");

            migrationBuilder.DropColumn(name: "tags", table: "rooms");

            migrationBuilder.DropColumn(name: "home_room_id", table: "player_settings");

            migrationBuilder.DropColumn(
                name: "navigator_left_pane_hidden",
                table: "player_settings"
            );

            migrationBuilder.DropColumn(name: "navigator_results_mode", table: "player_settings");

            migrationBuilder.DropColumn(name: "navigator_window_height", table: "player_settings");

            migrationBuilder.DropColumn(name: "navigator_window_width", table: "player_settings");

            migrationBuilder.DropColumn(name: "navigator_window_x", table: "player_settings");

            migrationBuilder.DropColumn(name: "navigator_window_y", table: "player_settings");

            migrationBuilder
                .AlterColumn<string>(
                    name: "figure",
                    table: "players",
                    type: "varchar(100)",
                    maxLength: 100,
                    nullable: false,
                    defaultValue: "hr-115-42.hd-195-19.ch-3030-82.lg-275-1408.fa-1201.ca-1804-64",
                    oldClrType: typeof(string),
                    oldType: "varchar(279)",
                    oldMaxLength: 279,
                    oldDefaultValue: "hr-115-42.hd-195-19.ch-3030-82.lg-275-1408.fa-1201.ca-1804-64"
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
