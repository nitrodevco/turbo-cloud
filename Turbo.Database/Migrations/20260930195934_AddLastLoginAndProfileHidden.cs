using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddLastLoginAndProfileHidden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_login",
                table: "players",
                type: "datetime(6)",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "profile_hidden",
                table: "player_settings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "last_login", table: "players");

            migrationBuilder.DropColumn(name: "profile_hidden", table: "player_settings");
        }
    }
}
