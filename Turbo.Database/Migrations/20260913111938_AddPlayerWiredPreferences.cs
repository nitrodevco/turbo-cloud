using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerWiredPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "room_chat_style_id", table: "players");

            migrationBuilder.AddColumn<bool>(
                name: "wired_inspect_button",
                table: "player_settings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "wired_menu_button",
                table: "player_settings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "wired_play_test_mode",
                table: "player_settings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "wired_show_all_notifications",
                table: "player_settings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder
                .AddColumn<string>(
                    name: "wired_ui_style",
                    table: "player_settings",
                    type: "varchar(50)",
                    maxLength: 50,
                    nullable: false,
                    defaultValue: ""
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "wired_variable_syntax_mode",
                table: "player_settings",
                type: "int",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.AddColumn<bool>(
                name: "wired_whisper_disabled",
                table: "player_settings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "wired_inspect_button", table: "player_settings");

            migrationBuilder.DropColumn(name: "wired_menu_button", table: "player_settings");

            migrationBuilder.DropColumn(name: "wired_play_test_mode", table: "player_settings");

            migrationBuilder.DropColumn(
                name: "wired_show_all_notifications",
                table: "player_settings"
            );

            migrationBuilder.DropColumn(name: "wired_ui_style", table: "player_settings");

            migrationBuilder.DropColumn(
                name: "wired_variable_syntax_mode",
                table: "player_settings"
            );

            migrationBuilder.DropColumn(name: "wired_whisper_disabled", table: "player_settings");

            migrationBuilder.AddColumn<int>(
                name: "room_chat_style_id",
                table: "players",
                type: "int",
                nullable: true
            );
        }
    }
}
