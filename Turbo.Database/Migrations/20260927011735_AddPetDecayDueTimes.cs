using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPetDecayDueTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "energy_decay_due_at",
                table: "pets",
                type: "datetime(6)",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "nutrition_decay_due_at",
                table: "pets",
                type: "datetime(6)",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "energy_decay_due_at", table: "pets");

            migrationBuilder.DropColumn(name: "nutrition_decay_due_at", table: "pets");
        }
    }
}
