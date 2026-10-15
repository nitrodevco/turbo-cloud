using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerDiscordPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "discord_allow_joining",
                table: "player_settings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "discord_hide_in_hidden_rooms",
                table: "player_settings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<int>(
                name: "discord_preferences_version",
                table: "player_settings",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<bool>(
                name: "discord_share_activity",
                table: "player_settings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "discord_show_habbo",
                table: "player_settings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "discord_allow_joining", table: "player_settings");
            migrationBuilder.DropColumn(
                name: "discord_hide_in_hidden_rooms",
                table: "player_settings"
            );
            migrationBuilder.DropColumn(
                name: "discord_preferences_version",
                table: "player_settings"
            );
            migrationBuilder.DropColumn(name: "discord_share_activity", table: "player_settings");
            migrationBuilder.DropColumn(name: "discord_show_habbo", table: "player_settings");
        }
    }
}
