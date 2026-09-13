using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "chat_bubble_type", table: "rooms");

            migrationBuilder.DropColumn(name: "chat_distance", table: "rooms");

            migrationBuilder.DropColumn(name: "chat_mode_type", table: "rooms");

            migrationBuilder.DropColumn(name: "chat_speed_type", table: "rooms");

            migrationBuilder
                .CreateTable(
                    name: "player_settings",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        generic_volume = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 100
                        ),
                        furni_volume = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 100
                        ),
                        trax_volume = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 100
                        ),
                        room_invites_ignored = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        room_camera_follow_disabled = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        ui_flags = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                        chat_style_id = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        chat_font_size = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        chat_mode = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        chat_bubble_width = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 1
                        ),
                        chat_scroll_speed = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 1
                        ),
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
                        table.PrimaryKey("PK_player_settings", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_settings_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_player_settings_player_id",
                table: "player_settings",
                column: "player_id",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "player_settings");

            migrationBuilder.AddColumn<int>(
                name: "chat_bubble_type",
                table: "rooms",
                type: "int",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.AddColumn<int>(
                name: "chat_distance",
                table: "rooms",
                type: "int",
                nullable: false,
                defaultValue: 50
            );

            migrationBuilder.AddColumn<int>(
                name: "chat_mode_type",
                table: "rooms",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "chat_speed_type",
                table: "rooms",
                type: "int",
                nullable: false,
                defaultValue: 1
            );
        }
    }
}
