using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomSettingsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "hidden_by_bc",
                table: "rooms",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "idle_autokick_enabled",
                table: "rooms",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<int>(
                name: "idle_autokick_timeout_seconds",
                table: "rooms",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<bool>(
                name: "idle_sleep_enabled",
                table: "rooms",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<int>(
                name: "idle_sleep_timeout_seconds",
                table: "rooms",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<bool>(
                name: "leave_on_door_tile",
                table: "rooms",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "mute_all_pets",
                table: "rooms",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<int>(
                name: "online_indicator_preference",
                table: "player_settings",
                type: "int",
                nullable: false,
                defaultValue: 0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "hidden_by_bc", table: "rooms");

            migrationBuilder.DropColumn(name: "idle_autokick_enabled", table: "rooms");

            migrationBuilder.DropColumn(name: "idle_autokick_timeout_seconds", table: "rooms");

            migrationBuilder.DropColumn(name: "idle_sleep_enabled", table: "rooms");

            migrationBuilder.DropColumn(name: "idle_sleep_timeout_seconds", table: "rooms");

            migrationBuilder.DropColumn(name: "leave_on_door_tile", table: "rooms");

            migrationBuilder.DropColumn(name: "mute_all_pets", table: "rooms");

            migrationBuilder.DropColumn(
                name: "online_indicator_preference",
                table: "player_settings"
            );
        }
    }
}
