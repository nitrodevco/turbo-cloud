using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class respects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_respect_reset",
                table: "players",
                type: "datetime(6)",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "pet_respect_left",
                table: "players",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "respect_left",
                table: "players",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "respect_replenishes_left",
                table: "players",
                type: "int",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.AddColumn<int>(
                name: "respect_total",
                table: "players",
                type: "int",
                nullable: false,
                defaultValue: 0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "last_respect_reset", table: "players");

            migrationBuilder.DropColumn(name: "pet_respect_left", table: "players");

            migrationBuilder.DropColumn(name: "respect_left", table: "players");

            migrationBuilder.DropColumn(name: "respect_replenishes_left", table: "players");

            migrationBuilder.DropColumn(name: "respect_total", table: "players");
        }
    }
}
